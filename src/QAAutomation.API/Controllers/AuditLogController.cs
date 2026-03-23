using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QAAutomation.API.Data;
using QAAutomation.API.DTOs;

namespace QAAutomation.API.Controllers;

/// <summary>
/// Returns paginated, filterable audit log entries scoped to a tenant project.
/// Covers PII/SPII protection events and all external API calls.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuditLogController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuditLogController(AppDbContext db) => _db = db;

    /// <summary>
    /// Returns paginated audit log entries.
    /// All parameters are optional filters.
    /// </summary>
    /// <param name="projectId">Tenant project ID. Required in production — returns all entries if omitted.</param>
    /// <param name="category">Filter by category: "PiiEvent" or "ExternalApiCall".</param>
    /// <param name="eventType">Filter by specific event type (e.g. "LlmAudit", "PiiRedacted").</param>
    /// <param name="outcome">Filter by outcome: "Success", "Failure", "Blocked", "Redacted", "Detected".</param>
    /// <param name="from">Return entries on or after this UTC date (yyyy-MM-dd).</param>
    /// <param name="to">Return entries on or before this UTC date (yyyy-MM-dd).</param>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Entries per page (default 50, max 200).</param>
    [HttpGet]
    [ProducesResponseType(typeof(AuditLogPageDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuditLogPageDto>> Get(
        [FromQuery] int? projectId = null,
        [FromQuery] string? category = null,
        [FromQuery] string? eventType = null,
        [FromQuery] string? outcome = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(1, page);

        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        if (projectId.HasValue)
            query = query.Where(e => e.ProjectId == projectId.Value);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(e => e.Category == category);

        if (!string.IsNullOrWhiteSpace(eventType))
            query = query.Where(e => e.EventType == eventType);

        if (!string.IsNullOrWhiteSpace(outcome))
            query = query.Where(e => e.Outcome == outcome);

        if (from.HasValue)
            query = query.Where(e => e.OccurredAt >= from.Value.ToUniversalTime());

        if (to.HasValue)
            query = query.Where(e => e.OccurredAt <= to.Value.ToUniversalTime().AddDays(1));

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(e => e.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new AuditLogEntryDto
            {
                Id = e.Id,
                ProjectId = e.ProjectId,
                Category = e.Category,
                EventType = e.EventType,
                Outcome = e.Outcome,
                Actor = e.Actor,
                PiiTypesDetected = e.PiiTypesDetected,
                HttpMethod = e.HttpMethod,
                Endpoint = e.Endpoint,
                HttpStatusCode = e.HttpStatusCode,
                DurationMs = e.DurationMs,
                Provider = e.Provider,
                Details = e.Details,
                OccurredAt = e.OccurredAt
            })
            .ToListAsync();

        return Ok(new AuditLogPageDto
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }
}
