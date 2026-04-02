using Microsoft.AspNetCore.Mvc;
using QAAutomation.Web.Models;
using QAAutomation.Web.Services;

namespace QAAutomation.Web.Controllers;

/// <summary>
/// Web UI controller for the end-to-end automated call QA pipeline.
/// Supports:
///   • Batch URL submission (recording or transcript URLs)
///   • Connector-based ingestion (SFTP, SharePoint, Verint, NICE, Ozonetel)
///   • Job monitoring and per-item result drill-down
/// All processing is fully automated — no human review required.
/// </summary>
public class CallPipelineController : ProjectAwareController
{
    private readonly ApiClient _api;

    public CallPipelineController(ApiClient api) => _api = api;

    // ── Index — list all pipeline jobs ────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var pid = CurrentProjectId > 0 ? (int?)CurrentProjectId : null;
        var jobs = await _api.GetPipelineJobs(pid);
        return View(jobs);
    }

    // ── Detail — single job with all items ───────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var job = await _api.GetPipelineJob(id);
        if (job == null) return NotFound();
        return View(job);
    }

    // ── Batch URL submission ──────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> BatchUrl()
    {
        var pid = CurrentProjectId > 0 ? (int?)CurrentProjectId : null;
        var forms = await _api.GetLegacyForms(pid);
        ViewBag.Forms = forms;
        return View(new CallPipelineBatchUrlViewModel
        {
            ProjectId = CurrentProjectId > 0 ? CurrentProjectId : null
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BatchUrl(CallPipelineBatchUrlViewModel model)
    {
        var pid = CurrentProjectId > 0 ? (int?)CurrentProjectId : null;
        if (!ModelState.IsValid)
        {
            ViewBag.Forms = await _api.GetLegacyForms(pid);
            return View(model);
        }

        // Parse URL list — one entry per line; optional pipe-delimited metadata
        // Format: url | agentName | callReference | callDate(ISO-8601)
        var items = model.UrlList
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(line =>
            {
                var parts = line.Split('|');
                return new
                {
                    url = parts[0].Trim(),
                    agentName = parts.Length > 1 ? parts[1].Trim() : (string?)null,
                    callReference = parts.Length > 2 ? parts[2].Trim() : (string?)null,
                    callDate = parts.Length > 3 && DateTime.TryParse(parts[3].Trim(), out var d) ? d : (DateTime?)null
                };
            })
            .Where(i => !string.IsNullOrWhiteSpace(i.url))
            .ToList();

        if (items.Count == 0)
        {
            ModelState.AddModelError("UrlList", "Please enter at least one URL.");
            ViewBag.Forms = await _api.GetLegacyForms(pid);
            return View(model);
        }

        var dto = new
        {
            name = model.Name,
            formId = model.FormId,
            projectId = CurrentProjectId > 0 ? CurrentProjectId : model.ProjectId,
            submittedBy = User.Identity?.Name ?? "web",
            items
        };

        var job = await _api.CreateBatchUrlPipelineJob(dto);
        if (job == null)
        {
            ModelState.AddModelError("", "Failed to create pipeline job. Please try again.");
            ViewBag.Forms = await _api.GetLegacyForms(pid);
            return View(model);
        }

        // Auto-trigger processing
        await _api.TriggerPipelineProcess(job.Id);
        TempData["Success"] = $"Pipeline job '{job.Name}' created with {items.Count} item(s) and queued for processing.";
        return RedirectToAction(nameof(Detail), new { id = job.Id });
    }

    // ── Connector-based job ───────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> FromConnector()
    {
        var pid = CurrentProjectId > 0 ? (int?)CurrentProjectId : null;
        var forms = await _api.GetLegacyForms(pid);
        ViewBag.Forms = forms;
        return View(new CallPipelineConnectorViewModel
        {
            ProjectId = CurrentProjectId > 0 ? CurrentProjectId : null
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FromConnector(CallPipelineConnectorViewModel model)
    {
        var pid = CurrentProjectId > 0 ? (int?)CurrentProjectId : null;
        if (!ModelState.IsValid)
        {
            ViewBag.Forms = await _api.GetLegacyForms(pid);
            return View(model);
        }

        var dto = new
        {
            name = model.Name,
            sourceType = model.SourceType,
            formId = model.FormId,
            projectId = CurrentProjectId > 0 ? CurrentProjectId : model.ProjectId,
            submittedBy = User.Identity?.Name ?? "web",
            sftpHost = model.SftpHost,
            sftpPort = model.SftpPort,
            sftpUsername = model.SftpUsername,
            sftpPassword = model.SftpPassword,
            sftpPath = model.SftpPath,
            sharePointSiteUrl = model.SharePointSiteUrl,
            sharePointClientId = model.SharePointClientId,
            sharePointClientSecret = model.SharePointClientSecret,
            sharePointLibraryName = model.SharePointLibraryName,
            recordingPlatformUrl = model.RecordingPlatformUrl,
            recordingPlatformApiKey = model.RecordingPlatformApiKey,
            recordingPlatformTenantId = model.RecordingPlatformTenantId,
            filterFromDate = model.FilterFromDate,
            filterToDate = model.FilterToDate
        };

        var job = await _api.CreateConnectorPipelineJob(dto);
        if (job == null)
        {
            ModelState.AddModelError("", "Failed to create connector pipeline job. Check connector settings and try again.");
            ViewBag.Forms = await _api.GetLegacyForms(pid);
            return View(model);
        }

        // Auto-trigger processing for the discovered items
        await _api.TriggerPipelineProcess(job.Id);
        TempData["Success"] = $"Pipeline job '{job.Name}' created with {job.TotalItems} discovered item(s) and queued for processing.";
        return RedirectToAction(nameof(Detail), new { id = job.Id });
    }

    // ── Manual re-process ─────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Process(int id)
    {
        await _api.TriggerPipelineProcess(id);
        TempData["Success"] = "Processing triggered. Refresh the page to see updated results.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    // ── Resume a stalled Running job ──────────────────────────────────────────

    /// <summary>
    /// Resets a pipeline job that is stuck in "Running" state (e.g. interrupted by an
    /// application restart) and re-triggers processing of the remaining pending items.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resume(int id)
    {
        await _api.ResumePipelineJob(id);
        TempData["Success"] = "Job resumed. Processing will continue from where it left off. Refresh the page to see progress.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    // ── File upload ───────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> FileUpload()
    {
        var pid = CurrentProjectId > 0 ? (int?)CurrentProjectId : null;
        ViewBag.Forms = await _api.GetLegacyForms(pid);
        return View(new CallPipelineFileUploadViewModel
        {
            ProjectId = CurrentProjectId > 0 ? CurrentProjectId : null
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> FileUpload(CallPipelineFileUploadViewModel model)
    {
        var pid = CurrentProjectId > 0 ? (int?)CurrentProjectId : null;

        if (model.File == null || model.File.Length == 0)
            ModelState.AddModelError("File", "Please choose a CSV or XLSX file.");

        if (!ModelState.IsValid)
        {
            ViewBag.Forms = await _api.GetLegacyForms(pid);
            return View(model);
        }

        await using var stream = model.File!.OpenReadStream();
        var (job, error) = await _api.UploadTranscriptFile(
            stream, model.File.FileName, model.Name, model.FormId,
            CurrentProjectId > 0 ? CurrentProjectId : model.ProjectId);

        if (job == null)
        {
            ModelState.AddModelError("", error ?? "Failed to create pipeline job. Please check the file format.");
            ViewBag.Forms = await _api.GetLegacyForms(pid);
            return View(model);
        }

        TempData["Success"] = $"Pipeline job '{job.Name}' created with {job.TotalItems} row(s) and is now processing.";
        return RedirectToAction(nameof(Detail), new { id = job.Id });
    }
}
