using Microsoft.AspNetCore.Mvc;
using QAAutomation.Web.Models;
using QAAutomation.Web.Services;
using System.Text.Json;

namespace QAAutomation.Web.Controllers;

/// <summary>
/// Controller for the automated audit module: transcript upload,
/// LLM-powered scoring review, and audit record creation.
/// </summary>
public class AutoAuditController : ProjectAwareController
{
    private readonly ApiClient _api;
    private readonly ILogger<AutoAuditController> _logger;
    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AutoAuditController(ApiClient api, ILogger<AutoAuditController> logger)
    {
        _api = api;
        _logger = logger;
    }

    // ──────────────────────────────────────────────────────────
    // GET /AutoAudit/Upload   —   Upload transcript page
    // ──────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Upload(int? formId)
    {
        var pid = CurrentProjectId > 0 ? (int?)CurrentProjectId : null;
        var forms = await _api.GetLegacyForms(pid);
        ViewBag.Forms = forms;
        var selectedId = formId ?? forms.FirstOrDefault()?.Id ?? 0;
        return View(new AutoAuditUploadViewModel
        {
            FormId = selectedId,
            EvaluatedBy = User.Identity?.Name ?? ""
        });
    }

    // ──────────────────────────────────────────────────────────
    // POST /AutoAudit/Upload  —  Accept file/text, call API LLM
    // ──────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(AutoAuditUploadViewModel model, IFormFile? transcriptFile)
    {
        // Resolve transcript text: file takes precedence over pasted text
        var transcript = string.Empty;
        if (transcriptFile != null && transcriptFile.Length > 0)
        {
            using var reader = new System.IO.StreamReader(transcriptFile.OpenReadStream());
            transcript = await reader.ReadToEndAsync();
        }
        else if (!string.IsNullOrWhiteSpace(model.TranscriptText))
        {
            transcript = model.TranscriptText;
        }

        if (string.IsNullOrWhiteSpace(transcript))
        {
            var pid = CurrentProjectId > 0 ? (int?)CurrentProjectId : null;
            var forms = await _api.GetLegacyForms(pid);
            ViewBag.Forms = forms;
            ModelState.AddModelError("", "Please provide a transcript — either upload a file or paste the text.");
            return View(model);
        }

        // Truncate to 12 000 chars to stay within typical context limits
        if (transcript.Length > 12000)
            transcript = transcript[..12000] + "\n[TRANSCRIPT TRUNCATED]";

        var auditRequest = new
        {
            formId = model.FormId,
            transcript,
            agentName = model.AgentName,
            callReference = model.CallReference,
            callDate = model.CallDate,
            evaluatedBy = model.EvaluatedBy
        };

        var sentimentRequest = new
        {
            transcript,
            agentName = model.AgentName,
            evaluatedBy = model.EvaluatedBy
        };

        // Run quality audit and sentiment analysis in parallel
        var reviewTask = _api.AutoAnalyze(auditRequest);
        var sentimentTask = _api.AnalyzeSentiment(sentimentRequest);
        await Task.WhenAll(reviewTask, sentimentTask);
        var review = await reviewTask;
        var sentiment = await sentimentTask;

        if (review == null)
        {
            var pid = CurrentProjectId > 0 ? (int?)CurrentProjectId : null;
            var forms = await _api.GetLegacyForms(pid);
            ViewBag.Forms = forms;
            ModelState.AddModelError("", "The analysis service returned an error. Please try again.");
            return View(model);
        }

        // Attach sentiment results to the review model
        review.Sentiment = sentiment;

        // Map API response to review view model and store in TempData for the Review step
        // (We store the json-serialized review so the Review page can edit scores)
        TempData["AutoAuditReview"] = JsonSerializer.Serialize(review);
        return RedirectToAction(nameof(Review));
    }

    // ──────────────────────────────────────────────────────────
    // GET /AutoAudit/Review   —   Review AI scores before saving
    // ──────────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult Review()
    {
        if (TempData["AutoAuditReview"] is not string json)
            return RedirectToAction(nameof(Upload));

        // Keep TempData alive so user can go back (refresh won't blow away scores)
        TempData.Keep("AutoAuditReview");

        var review = JsonSerializer.Deserialize<AutoAuditReviewViewModel>(json, _jsonOpts);
        if (review == null)
            return RedirectToAction(nameof(Upload));

        // Pre-serialize fields with camelCase naming for the JavaScript score-preview panel
        ViewData["FieldsJson"] = JsonSerializer.Serialize(
            review.Fields,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        return View(review);
    }

    // ──────────────────────────────────────────────────────────
    // POST /AutoAudit/Save    —   Save (possibly adjusted) scores
    // ──────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(string reviewJson)
    {
        if (string.IsNullOrEmpty(reviewJson))
            return RedirectToAction(nameof(Upload));

        AutoAuditReviewViewModel? review;
        try { review = JsonSerializer.Deserialize<AutoAuditReviewViewModel>(reviewJson, _jsonOpts); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize auto-audit review");
            return RedirectToAction(nameof(Upload));
        }

        if (review == null)
            return RedirectToAction(nameof(Upload));

        var scores = review.Fields.Select(f => new
        {
            fieldId = f.FieldId,
            value = f.FinalScore.ToString(System.Globalization.CultureInfo.InvariantCulture),
            numericValue = f.FinalScore
        }).ToList();

        var notes = $"[Auto-Audit] {review.OverallReasoning}";
        if (review.Sentiment != null && !string.IsNullOrWhiteSpace(review.Sentiment.OverallInsight))
            notes += $"\n[Sentiment] {review.Sentiment.OverallInsight}";

        // Serialize structured AI data so it can be displayed in the saved audit detail view
        string? sentimentJson = review.Sentiment != null
            ? JsonSerializer.Serialize(review.Sentiment)
            : null;
        string? fieldReasoningJson = review.Fields.Any(f => !string.IsNullOrEmpty(f.Reasoning))
            ? JsonSerializer.Serialize(review.Fields.Where(f => !string.IsNullOrEmpty(f.Reasoning))
                .Select(f => new { fieldId = f.FieldId, reasoning = f.Reasoning }))
            : null;

        var dto = new
        {
            formId = review.FormId,
            evaluatedBy = review.EvaluatedBy,
            agentName = review.AgentName,
            callReference = review.CallReference,
            callDate = review.CallDate,
            notes,
            overallReasoning = review.OverallReasoning,
            sentimentJson,
            fieldReasoningJson,
            scores
        };

        var auditId = await _api.CreateAudit(dto);

        // ── Auto-create TNI if score is not 100% ─────────────────────────────
        if (auditId.HasValue && review.ScorePercent < 100 && !string.IsNullOrWhiteSpace(review.AgentName))
        {
            var failedFields = review.Fields
                .Where(f => f.MaxRating > 0 && f.FinalScore < f.MaxRating)
                .OrderBy(f => f.SectionTitle).ThenBy(f => f.FieldLabel)
                .Select((f, idx) => new
                {
                    targetArea = f.SectionTitle,
                    itemType = "Recommendation",
                    content = !string.IsNullOrWhiteSpace(f.Reasoning)
                        ? $"[{f.FieldLabel}] {f.Reasoning}"
                        : $"[{f.FieldLabel}] Scored {f.FinalScore}/{f.MaxRating} – coach agent on this area.",
                    order = idx + 1
                }).ToList();

            if (failedFields.Any())
            {
                var pid = CurrentProjectId > 0 ? (int?)CurrentProjectId : null;
                var tniDto = new
                {
                    title = $"TNI – {review.AgentName} – {DateTime.Today:yyyy-MM-dd}",
                    description = review.OverallReasoning,
                    agentName = review.AgentName,
                    trainerName = review.EvaluatedBy,
                    trainerUsername = review.EvaluatedBy,
                    dueDate = DateTime.Today.AddDays(30),
                    projectId = pid,
                    evaluationResultId = auditId,
                    createdBy = review.EvaluatedBy,
                    isAutoGenerated = true,
                    items = failedFields
                };
                await _api.CreateTrainingPlan(tniDto);
            }
        }

        return RedirectToAction("Index", "Audit");
    }
}
