using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QAAutomation.API.Data;
using QAAutomation.API.DTOs;
using QAAutomation.API.Models;

namespace QAAutomation.API.Controllers;

/// <summary>
/// CRUD for <see cref="SamplingPolicy"/> records.
/// Applying a policy selects eligible <see cref="EvaluationResult"/> records and
/// enqueues them for human review.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SamplingPoliciesController : ControllerBase
{
    private readonly AppDbContext _db;

    public SamplingPoliciesController(AppDbContext db) => _db = db;

    // ── GET /api/samplingpolicies ─────────────────────────────────────────────

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SamplingPolicyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SamplingPolicyDto>>> GetAll(
        [FromQuery] int? projectId = null)
    {
        var query = _db.SamplingPolicies.AsQueryable();
        if (projectId.HasValue)
            query = query.Where(p => p.ProjectId == null || p.ProjectId == projectId.Value);
        var policies = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
        return Ok(policies.Select(ToDto));
    }

    // ── GET /api/samplingpolicies/{id} ────────────────────────────────────────

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(SamplingPolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SamplingPolicyDto>> GetById(int id)
    {
        var p = await _db.SamplingPolicies.FindAsync(id);
        return p is null ? NotFound() : Ok(ToDto(p));
    }

    // ── POST /api/samplingpolicies ────────────────────────────────────────────

    [HttpPost]
    [ProducesResponseType(typeof(SamplingPolicyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SamplingPolicyDto>> Create([FromBody] CreateSamplingPolicyDto dto)
    {
        if (!IsValidMethod(dto.SamplingMethod))
            return BadRequest("SamplingMethod must be 'Percentage' or 'Count'.");
        if (dto.SamplingMethod == "Percentage" && (dto.SampleValue < 0 || dto.SampleValue > 100))
            return BadRequest("SampleValue must be between 0 and 100 for Percentage sampling.");
        if (dto.SamplingMethod == "Count" && dto.SampleValue < 1)
            return BadRequest("SampleValue must be at least 1 for Count sampling.");

        var policy = new SamplingPolicy
        {
            Name = dto.Name,
            Description = dto.Description,
            ProjectId = dto.ProjectId,
            CallTypeFilter = dto.CallTypeFilter,
            MinDurationSeconds = dto.MinDurationSeconds,
            MaxDurationSeconds = dto.MaxDurationSeconds,
            SamplingMethod = dto.SamplingMethod,
            SampleValue = dto.SampleValue,
            IsActive = dto.IsActive,
            CreatedBy = dto.CreatedBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.SamplingPolicies.Add(policy);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = policy.Id }, ToDto(policy));
    }

    // ── PUT /api/samplingpolicies/{id} ────────────────────────────────────────

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(SamplingPolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SamplingPolicyDto>> Update(int id, [FromBody] UpdateSamplingPolicyDto dto)
    {
        var policy = await _db.SamplingPolicies.FindAsync(id);
        if (policy is null) return NotFound();
        if (!IsValidMethod(dto.SamplingMethod))
            return BadRequest("SamplingMethod must be 'Percentage' or 'Count'.");

        policy.Name = dto.Name;
        policy.Description = dto.Description;
        policy.ProjectId = dto.ProjectId;
        policy.CallTypeFilter = dto.CallTypeFilter;
        policy.MinDurationSeconds = dto.MinDurationSeconds;
        policy.MaxDurationSeconds = dto.MaxDurationSeconds;
        policy.SamplingMethod = dto.SamplingMethod;
        policy.SampleValue = dto.SampleValue;
        policy.IsActive = dto.IsActive;
        policy.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(ToDto(policy));
    }

    // ── DELETE /api/samplingpolicies/{id} ─────────────────────────────────────

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var policy = await _db.SamplingPolicies.FindAsync(id);
        if (policy is null) return NotFound();
        _db.SamplingPolicies.Remove(policy);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── POST /api/samplingpolicies/{id}/apply ─────────────────────────────────

    /// <summary>
    /// Applies a sampling policy: selects eligible <see cref="EvaluationResult"/> records
    /// that have not yet been enqueued, and creates <see cref="HumanReviewItem"/> entries
    /// for the sampled subset.
    /// </summary>
    [HttpPost("{id}/apply")]
    [ProducesResponseType(typeof(SamplingApplyResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SamplingApplyResultDto>> Apply(int id, [FromQuery] string? appliedBy = null)
    {
        var policy = await _db.SamplingPolicies.FindAsync(id);
        if (policy is null) return NotFound();

        // IDs already in the review queue
        var alreadyQueuedIds = (await _db.HumanReviewItems
            .Select(r => r.EvaluationResultId)
            .ToListAsync()).ToHashSet();

        // Fetch candidate results
        var query = _db.EvaluationResults
            .Include(r => r.Form)
            .AsQueryable();

        if (policy.ProjectId.HasValue)
            query = query.Where(r => r.Form.Lob != null && r.Form.Lob.ProjectId == policy.ProjectId.Value);

        if (!string.IsNullOrWhiteSpace(policy.CallTypeFilter))
            query = query.Where(r => r.Form.Name.Contains(policy.CallTypeFilter));

        if (policy.MinDurationSeconds.HasValue)
            query = query.Where(r => r.CallDurationSeconds.HasValue && r.CallDurationSeconds >= policy.MinDurationSeconds);

        if (policy.MaxDurationSeconds.HasValue)
            query = query.Where(r => r.CallDurationSeconds.HasValue && r.CallDurationSeconds <= policy.MaxDurationSeconds);

        var candidates = await query.Select(r => r.Id).ToListAsync();
        var eligible = candidates.Where(cid => !alreadyQueuedIds.Contains(cid)).ToList();

        int sampleCount = policy.SamplingMethod == "Percentage"
            ? (int)Math.Ceiling(eligible.Count * policy.SampleValue / 100.0)
            : (int)Math.Min(policy.SampleValue, eligible.Count);

        // Deterministic random sample — shuffle then take
        var sampled = eligible.OrderBy(_ => Random.Shared.Next()).Take(sampleCount).ToList();

        var now = DateTime.UtcNow;
        foreach (var resultId in sampled)
        {
            _db.HumanReviewItems.Add(new HumanReviewItem
            {
                EvaluationResultId = resultId,
                SamplingPolicyId = policy.Id,
                SampledAt = now,
                SampledBy = appliedBy ?? "system",
                Status = "Pending"
            });
        }
        await _db.SaveChangesAsync();

        return Ok(new SamplingApplyResultDto
        {
            PolicyId = policy.Id,
            PolicyName = policy.Name,
            EligibleCount = eligible.Count,
            SampledCount = sampled.Count,
            AlreadySampledCount = candidates.Count - eligible.Count
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsValidMethod(string? method) =>
        method is "Percentage" or "Count";

    private static SamplingPolicyDto ToDto(SamplingPolicy p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        ProjectId = p.ProjectId,
        CallTypeFilter = p.CallTypeFilter,
        MinDurationSeconds = p.MinDurationSeconds,
        MaxDurationSeconds = p.MaxDurationSeconds,
        SamplingMethod = p.SamplingMethod,
        SampleValue = p.SampleValue,
        IsActive = p.IsActive,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt,
        CreatedBy = p.CreatedBy
    };
}
