using Microsoft.AspNetCore.Mvc;
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
    private readonly ILogger<CallPipelineController> _logger;

    public CallPipelineController(ICallPipelineService svc, ILogger<CallPipelineController> logger)
    {
        _svc = svc;
        _logger = logger;
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
        // For larger batches fire-and-forget and return 202.
        if (job.TotalItems <= 5)
        {
            await _svc.ProcessJobAsync(id, HttpContext.RequestAborted);
            var updated = await _svc.GetJobAsync(id);
            return Ok(updated);
        }

        // Large batch — kick off in background and return accepted
        _ = Task.Run(async () =>
        {
            try { await _svc.ProcessJobAsync(id); }
            catch (Exception ex) { _logger.LogError(ex, "Background processing of job {Id} failed", id); }
        });

        return Accepted(new { message = $"Job {id} queued for background processing.", jobId = id });
    }
}
