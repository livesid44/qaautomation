using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QAAutomation.API.Data;
using QAAutomation.API.DTOs;
using QAAutomation.API.Models;

namespace QAAutomation.API.Controllers;

/// <summary>
/// Manages the human review queue.
/// QA analysts see items assigned to them (or unassigned) and submit their verdict.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HumanReviewController : ControllerBase
{
    private readonly AppDbContext _db;

    public HumanReviewController(AppDbContext db) => _db = db;

    // ── GET /api/humanreview ──────────────────────────────────────────────────

    /// <summary>
    /// Lists review queue items.
    /// Optional filters: ?status=Pending|InReview|Reviewed, ?assignedTo=username, ?projectId=N
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<HumanReviewItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<HumanReviewItemDto>>> GetAll(
        [FromQuery] string? status = null,
        [FromQuery] string? assignedTo = null,
        [FromQuery] int? projectId = null)
    {
        var query = _db.HumanReviewItems
            .Include(r => r.EvaluationResult)
                .ThenInclude(e => e!.Form)
                    .ThenInclude(f => f.Lob)
            .Include(r => r.SamplingPolicy)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);

        if (!string.IsNullOrWhiteSpace(assignedTo))
            query = query.Where(r => r.AssignedTo == assignedTo || r.AssignedTo == null);

        if (projectId.HasValue)
            query = query.Where(r =>
                r.EvaluationResult != null &&
                r.EvaluationResult.Form.Lob != null &&
                r.EvaluationResult.Form.Lob.ProjectId == projectId.Value);

        var items = await query.OrderByDescending(r => r.SampledAt).ToListAsync();
        return Ok(items.Select(ToDto));
    }

    // ── GET /api/humanreview/{id} ─────────────────────────────────────────────

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(HumanReviewItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HumanReviewItemDto>> GetById(int id)
    {
        var item = await _db.HumanReviewItems
            .Include(r => r.EvaluationResult)
                .ThenInclude(e => e!.Form)
                    .ThenInclude(f => f.Lob)
            .Include(r => r.SamplingPolicy)
            .FirstOrDefaultAsync(r => r.Id == id);
        return item is null ? NotFound() : Ok(ToDto(item));
    }

    // ── POST /api/humanreview/manual ──────────────────────────────────────────

    /// <summary>
    /// Manually adds an evaluation result to the human review queue.
    /// </summary>
    [HttpPost("manual")]
    [ProducesResponseType(typeof(HumanReviewItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<HumanReviewItemDto>> AddManual(
        [FromBody] AddManualReviewDto dto)
    {
        // Check evaluation result exists
        var exists = await _db.EvaluationResults.AnyAsync(e => e.Id == dto.EvaluationResultId);
        if (!exists)
            return BadRequest($"EvaluationResult {dto.EvaluationResultId} not found.");

        // Prevent duplicates
        var alreadyQueued = await _db.HumanReviewItems
            .AnyAsync(r => r.EvaluationResultId == dto.EvaluationResultId);
        if (alreadyQueued)
            return Conflict("This evaluation result is already in the review queue.");

        var item = new HumanReviewItem
        {
            EvaluationResultId = dto.EvaluationResultId,
            SampledBy = dto.AddedBy ?? "manual",
            SampledAt = DateTime.UtcNow,
            AssignedTo = dto.AssignedTo,
            Status = "Pending"
        };
        _db.HumanReviewItems.Add(item);
        await _db.SaveChangesAsync();

        await LoadNavigations(item);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, ToDto(item));
    }

    // ── PUT /api/humanreview/{id}/start ──────────────────────────────────────

    /// <summary>Marks an item as InReview (a QA user has opened it).</summary>
    [HttpPut("{id}/start")]
    [ProducesResponseType(typeof(HumanReviewItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HumanReviewItemDto>> Start(int id, [FromQuery] string? reviewer = null)
    {
        var item = await _db.HumanReviewItems.FindAsync(id);
        if (item is null) return NotFound();
        if (item.Status == "Pending")
        {
            item.Status = "InReview";
            if (!string.IsNullOrWhiteSpace(reviewer))
                item.AssignedTo ??= reviewer;
            await _db.SaveChangesAsync();
        }
        await LoadNavigations(item);
        return Ok(ToDto(item));
    }

    // ── PUT /api/humanreview/{id}/review ─────────────────────────────────────

    /// <summary>Submit the human verdict for a review queue item.</summary>
    [HttpPut("{id}/review")]
    [ProducesResponseType(typeof(HumanReviewItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HumanReviewItemDto>> SubmitReview(int id, [FromBody] SubmitReviewDto dto)
    {
        if (!IsValidVerdict(dto.ReviewVerdict))
            return BadRequest("ReviewVerdict must be 'Agree', 'Disagree', or 'Partial'.");

        var item = await _db.HumanReviewItems.FindAsync(id);
        if (item is null) return NotFound();

        item.Status = "Reviewed";
        item.ReviewerComment = dto.ReviewerComment;
        item.ReviewVerdict = dto.ReviewVerdict;
        item.ReviewedBy = dto.ReviewedBy;
        item.ReviewedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await LoadNavigations(item);
        return Ok(ToDto(item));
    }

    // ── DELETE /api/humanreview/{id} ──────────────────────────────────────────

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.HumanReviewItems.FindAsync(id);
        if (item is null) return NotFound();
        _db.HumanReviewItems.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsValidVerdict(string? v) => v is "Agree" or "Disagree" or "Partial";

    private async Task LoadNavigations(HumanReviewItem item)
    {
        await _db.Entry(item).Reference(i => i.EvaluationResult).LoadAsync();
        if (item.EvaluationResult != null)
        {
            await _db.Entry(item.EvaluationResult).Reference(e => e.Form).LoadAsync();
            if (item.EvaluationResult.Form?.Lob != null)
                await _db.Entry(item.EvaluationResult.Form).Reference(f => f.Lob).LoadAsync();
        }
        if (item.SamplingPolicyId.HasValue)
            await _db.Entry(item).Reference(i => i.SamplingPolicy).LoadAsync();
    }

    private static HumanReviewItemDto ToDto(HumanReviewItem item)
    {
        var result = item.EvaluationResult;
        double? scorePercent = null;
        if (result != null && result.Scores.Any())
        {
            var method = result.Form?.ScoringMethod ?? Models.ScoringMethod.Generic;
            var entries = result.Scores
                .Where(s => s.Field?.Section != null && s.Field.FieldType == Models.FieldType.Rating)
                .Select(s => new QAAutomation.API.Services.ScoringCalculator.FieldEntry(
                    s.Field.Section.Id,
                    s.Field.Section.Title,
                    s.Field.Section.Order,
                    s.NumericValue ?? 0,
                    s.Field.MaxRating));
            var (totalScore, maxScore, _) = QAAutomation.API.Services.ScoringCalculator.Compute(method, entries);
            scorePercent = maxScore > 0 ? Math.Round(totalScore / maxScore * 100, 1) : 0;
        }

        return new HumanReviewItemDto
        {
            Id = item.Id,
            EvaluationResultId = item.EvaluationResultId,
            SamplingPolicyId = item.SamplingPolicyId,
            SamplingPolicyName = item.SamplingPolicy?.Name,
            SampledAt = item.SampledAt,
            SampledBy = item.SampledBy,
            AssignedTo = item.AssignedTo,
            Status = item.Status,
            ReviewerComment = item.ReviewerComment,
            ReviewVerdict = item.ReviewVerdict,
            ReviewedBy = item.ReviewedBy,
            ReviewedAt = item.ReviewedAt,
            // AI audit summary from EvaluationResult
            AgentName = result?.AgentName,
            CallReference = result?.CallReference,
            CallDate = result?.CallDate,
            FormName = result?.Form?.Name,
            AiScorePercent = scorePercent,
            AiReasoning = result?.Notes,
            ProjectId = result?.Form?.Lob?.ProjectId
        };
    }
}

/// <summary>Request body for manually adding an evaluation result to the review queue.</summary>
public class AddManualReviewDto
{
    public int EvaluationResultId { get; set; }
    public string? AssignedTo { get; set; }
    public string? AddedBy { get; set; }
}
