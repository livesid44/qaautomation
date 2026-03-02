using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QAAutomation.API.Data;
using QAAutomation.API.DTOs;

namespace QAAutomation.API.Controllers;

/// <summary>Returns pre-aggregated analytics data for the QA dashboard.</summary>
[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AnalyticsController(AppDbContext db) => _db = db;

    /// <summary>Returns aggregated QA analytics: daily scores, agent performance, parameter trends, and call-type trends.
    /// Pass ?projectId=N to restrict analysis to a single project.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(AnalyticsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AnalyticsDto>> Get([FromQuery] int? projectId = null)
    {
        // Load all results with scores + field metadata in a single query
        var query = _db.EvaluationResults
            .Include(r => r.Form)
                .ThenInclude(f => f.Lob)
            .Include(r => r.Scores)
                .ThenInclude(s => s.Field)
                    .ThenInclude(f => f.Section)
            .AsNoTracking()
            .AsQueryable();

        if (projectId.HasValue)
            query = query.Where(r => r.Form.Lob != null && r.Form.Lob.ProjectId == projectId.Value);

        var results = await query.ToListAsync();

        if (results.Count == 0)
            return Ok(new AnalyticsDto());

        // Helper: score % for a single result
        static double ScorePercent(global::QAAutomation.API.Models.EvaluationResult r)
        {
            var max = r.Scores
                .Where(s => s.Field?.FieldType == global::QAAutomation.API.Models.FieldType.Rating)
                .Sum(s => s.Field.MaxRating);
            var total = r.Scores.Sum(s => s.NumericValue ?? 0);
            return max > 0 ? Math.Round(total / max * 100, 1) : 0;
        }

        // ── Daily scores ─────────────────────────────────────────────────────
        var daily = results
            .GroupBy(r => (r.CallDate ?? r.EvaluatedAt).Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyScoreDto
            {
                Date = g.Key.ToString("yyyy-MM-dd"),
                AvgScorePercent = Math.Round(g.Average(ScorePercent), 1),
                AuditCount = g.Count()
            }).ToList();

        // ── Agent scores ─────────────────────────────────────────────────────
        var agents = results
            .Where(r => !string.IsNullOrWhiteSpace(r.AgentName))
            .GroupBy(r => r.AgentName!)
            .OrderByDescending(g => g.Count())
            .Select(g => new AgentScoreDto
            {
                AgentName = g.Key,
                AvgScorePercent = Math.Round(g.Average(ScorePercent), 1),
                AuditCount = g.Count()
            }).ToList();

        // ── Parameter trends (field-level averages) ──────────────────────────
        var paramTrends = results
            .SelectMany(r => r.Scores
                .Where(s => s.Field != null && s.Field.MaxRating > 0 && s.NumericValue.HasValue)
                .Select(s => new
                {
                    Label = s.Field.Label,
                    Section = s.Field.Section?.Title ?? "",
                    ScorePct = Math.Round(s.NumericValue!.Value / s.Field.MaxRating * 100, 1)
                }))
            .GroupBy(x => new { x.Label, x.Section })
            .Select(g => new ParameterTrendDto
            {
                ParameterLabel = g.Key.Label,
                SectionTitle = g.Key.Section,
                AvgScorePercent = Math.Round(g.Average(x => x.ScorePct), 1),
                ScoredCount = g.Count()
            })
            .OrderBy(p => p.SectionTitle).ThenBy(p => p.ParameterLabel)
            .ToList();

        // ── Call-type (form) trends ───────────────────────────────────────────
        var callTypes = results
            .GroupBy(r => r.Form?.Name ?? "Unknown")
            .OrderByDescending(g => g.Count())
            .Select(g => new CallTypeScoreDto
            {
                FormName = g.Key,
                AvgScorePercent = Math.Round(g.Average(ScorePercent), 1),
                AuditCount = g.Count()
            }).ToList();

        return Ok(new AnalyticsDto
        {
            TotalAudits = results.Count,
            DailyScores = daily,
            AgentScores = agents,
            ParameterTrends = paramTrends,
            CallTypeScores = callTypes
        });
    }
}
