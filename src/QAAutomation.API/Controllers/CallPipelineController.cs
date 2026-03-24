using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using QAAutomation.API.DTOs;
using QAAutomation.API.Services;

namespace QAAutomation.API.Controllers;

/// <summary>
/// End-to-end automated call QA pipeline.
/// Supports batch-URL submission, SFTP, SharePoint, and recording platforms
/// (Verint, NICE CXOne, Ozonetel) as transcript sources.
/// Processing is fully automated — no human review step required.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[IgnoreAntiforgeryToken]   // REST API — callers authenticate via bearer tokens, not cookies
public class CallPipelineController : ControllerBase
{
    private readonly ICallPipelineService _svc;
    private readonly PipelineProgressHub _progressHub;
    private readonly ILogger<CallPipelineController> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public CallPipelineController(
        ICallPipelineService svc,
        PipelineProgressHub progressHub,
        ILogger<CallPipelineController> logger,
        IServiceScopeFactory scopeFactory)
    {
        _svc = svc;
        _progressHub = progressHub;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    // ── List jobs ─────────────────────────────────────────────────────────────

    /// <summary>List all pipeline jobs, optionally filtered by project.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<CallPipelineJobDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CallPipelineJobDto>>> GetAll([FromQuery] int? projectId = null)
        => Ok(await _svc.ListJobsAsync(projectId));

    /// <summary>Get a single pipeline job with all its items.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CallPipelineJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CallPipelineJobDto>> GetById(int id)
    {
        var job = await _svc.GetJobAsync(id);
        return job is null ? NotFound() : Ok(job);
    }

    // ── Create jobs ───────────────────────────────────────────────────────────

    /// <summary>
    /// Submit a batch of recording/transcript URLs for fully automated QA analysis.
    /// Each URL is fetched by the pipeline; the transcript is scored by the LLM
    /// against the specified evaluation form and an EvaluationResult is persisted.
    /// Call POST /api/callpipeline/{id}/process to trigger processing immediately,
    /// or let the background scheduler pick it up.
    /// </summary>
    [HttpPost("batch-urls")]
    [ProducesResponseType(typeof(CallPipelineJobDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CallPipelineJobDto>> CreateBatchUrl([FromBody] CreateBatchUrlJobDto dto)
    {
        if (dto.Items.Count == 0)
            return BadRequest("At least one URL item is required.");

        var job = await _svc.CreateBatchUrlJobAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = job.Id }, job);
    }

    /// <summary>
    /// Create a pipeline job sourced from an external connector:
    /// SFTP directory, SharePoint library, Verint, NICE CXOne, or Ozonetel.
    /// The service immediately discovers available transcripts/recordings
    /// and queues them as Pending items.
    /// </summary>
    [HttpPost("from-connector")]
    [ProducesResponseType(typeof(CallPipelineJobDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CallPipelineJobDto>> CreateFromConnector([FromBody] CreateConnectorJobDto dto)
    {
        var validTypes = new[] { "SFTP", "SharePoint", "Verint", "NICE", "Ozonetel" };
        if (!validTypes.Contains(dto.SourceType))
            return BadRequest($"SourceType must be one of: {string.Join(", ", validTypes)}");

        var job = await _svc.CreateConnectorJobAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = job.Id }, job);
    }

    // ── Trigger processing ────────────────────────────────────────────────────

    /// <summary>
    /// Trigger processing of all pending items in the specified job.
    /// Processing runs synchronously in the request for small batches.
    /// For large batches this call returns 202 Accepted immediately and
    /// processing continues in a background Task.
    /// </summary>
    [HttpPost("{id:int}/process")]
    [ProducesResponseType(typeof(CallPipelineJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CallPipelineJobDto>> Process(int id)
    {
        var job = await _svc.GetJobAsync(id);
        if (job is null) return NotFound();
        if (job.Status == "Running") return Conflict("Job is already running.");

        // For small batches (≤ 5 items) process inline so the caller gets the result immediately.
        // Use CancellationToken.None: the LLM call can take several minutes for large transcripts
        // and must not be cancelled if the browser connection drops or a proxy timeout fires.
        // For larger batches fire-and-forget and return 202.
        if (job.TotalItems <= 5)
        {
            await _svc.ProcessJobAsync(id, CancellationToken.None);
            var updated = await _svc.GetJobAsync(id);
            return Ok(updated);
        }

        // Large batch — kick off in background and return accepted
        var jobId = id;
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<ICallPipelineService>();
            try { await svc.ProcessJobAsync(jobId); }
            catch (Exception ex) { _logger.LogError(ex, "Background processing of job {Id} failed", jobId); }
        });

        return Accepted(new { message = $"Job {id} queued for background processing.", jobId = id });
    }

    // ── File upload ───────────────────────────────────────────────────────────

    /// <summary>
    /// Accepts a multipart/form-data upload of a CSV or XLSX file containing
    /// call transcripts.  Each row becomes one pipeline item that is
    /// auto-audited against the selected evaluation form.
    ///
    /// Expected columns (case-insensitive):
    ///   transcript (required), agentName, callReference, callDate
    ///
    /// After creation the job is automatically queued for background processing.
    /// </summary>
    [HttpPost("upload-file")]
    [RequestSizeLimit(52_428_800)] // 50 MB
    [ProducesResponseType(typeof(CallPipelineJobDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CallPipelineJobDto>> UploadFile(
        IFormFile file,
        [FromForm] string name,
        [FromForm] int formId,
        [FromForm] int? projectId,
        [FromForm] string? submittedBy)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file uploaded.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".csv" or ".xlsx" or ".tsv" or ".txt"))
            return BadRequest("Only CSV (.csv, .tsv) and Excel (.xlsx) files are supported.");

        List<BatchUrlItemDto> items;
        try
        {
            await using var stream = file.OpenReadStream();
            items = FileUploadParserService.Parse(stream, file.FileName);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File upload parse failed for {FileName}", file.FileName);
            return BadRequest("Could not parse the uploaded file. Ensure it is a valid CSV or XLSX.");
        }

        var jobName = string.IsNullOrWhiteSpace(name)
            ? $"Upload: {Path.GetFileNameWithoutExtension(file.FileName)}"
            : name;

        var job = await _svc.CreateFileUploadJobAsync(
            jobName, formId, projectId, submittedBy ?? "web", items, HttpContext.RequestAborted);

        // Always process in background so the HTTP response returns immediately
        var uploadedJobId = job.Id;
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<ICallPipelineService>();
            try { await svc.ProcessJobAsync(uploadedJobId); }
            catch (Exception ex) { _logger.LogError(ex, "Background processing of uploaded job {Id} failed", uploadedJobId); }
        });

        return CreatedAtAction(nameof(GetById), new { id = job.Id }, job);
    }

    // ── SSE progress stream ───────────────────────────────────────────────────

    /// <summary>
    /// Server-Sent Events endpoint.  Opens a persistent connection and streams
    /// one JSON event per completed pipeline item.  The connection is closed
    /// automatically when the job finishes.
    ///
    /// Event format: "data: {json}\n\n"
    /// The browser should create an EventSource pointed at this URL.
    /// </summary>
    [HttpGet("{id:int}/progress")]
    public async Task Progress(int id, CancellationToken ct)
    {
        var job = await _svc.GetJobAsync(id);
        if (job is null) { Response.StatusCode = 404; return; }

        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no"); // for Nginx reverse proxies

        // If the job is already finished, send a single terminal event and close
        if (job.Status is "Completed" or "Failed")
        {
            var terminal = new PipelineProgressEventDto
            {
                JobId        = job.Id,
                ItemStatus   = "Done",
                CompletedSoFar = job.CompletedItems,
                TotalItems   = job.TotalItems,
                JobStatus    = job.Status
            };
            await WriteSSEAsync(terminal, ct);
            return;
        }

        var channel = _progressHub.Subscribe(id);
        try
        {
            await foreach (var evt in channel.Reader.ReadAllAsync(ct))
            {
                await WriteSSEAsync(evt, ct);
                await Response.Body.FlushAsync(ct);
                if (evt.JobStatus is "Completed" or "Failed") break;
            }
        }
        catch (OperationCanceledException) { /* client disconnected */ }
        finally
        {
            _progressHub.Unsubscribe(id, channel);
        }
    }

    private static readonly JsonSerializerOptions _sseJson =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private async Task WriteSSEAsync(PipelineProgressEventDto evt, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(evt, _sseJson);
        await Response.WriteAsync($"data: {json}\n\n", ct);
    }
}
