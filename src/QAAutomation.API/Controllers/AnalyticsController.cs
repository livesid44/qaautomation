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

    // ── GET /api/analytics/explainability ─────────────────────────────────────

    /// <summary>
    /// Returns explainability analytics for audit decisions: decision drivers, signal usage,
    /// HITL agreement rates, and failure reason analysis.
    /// Pass ?projectId=N to restrict analysis to a single project.
    /// </summary>
    [HttpGet("explainability")]
    [ProducesResponseType(typeof(ExplainabilityDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExplainabilityDto>> GetExplainability([FromQuery] int? projectId = null)
    {
        // Load evaluation results with all scores and field metadata
        var resultQuery = _db.EvaluationResults
            .Include(r => r.Form)
                .ThenInclude(f => f.Lob)
            .Include(r => r.Scores)
                .ThenInclude(s => s.Field)
                    .ThenInclude(f => f.Section)
            .AsNoTracking()
            .AsQueryable();

        if (projectId.HasValue)
            resultQuery = resultQuery.Where(r => r.Form.Lob != null && r.Form.Lob.ProjectId == projectId.Value);

        var results = await resultQuery.ToListAsync();

        // Load HITL review items scoped to the same project
        var reviewQuery = _db.HumanReviewItems
            .Include(h => h.SamplingPolicy)
            .Include(h => h.EvaluationResult)
                .ThenInclude(e => e!.Form)
                    .ThenInclude(f => f.Lob)
            .Where(h => h.Status == "Reviewed" && h.ReviewVerdict != null)
            .AsNoTracking()
            .AsQueryable();

        if (projectId.HasValue)
            reviewQuery = reviewQuery.Where(h =>
                h.EvaluationResult != null &&
                h.EvaluationResult.Form.Lob != null &&
                h.EvaluationResult.Form.Lob.ProjectId == projectId.Value);

        var reviewItems = await reviewQuery.ToListAsync();

        if (results.Count == 0)
            return Ok(new ExplainabilityDto());

        // Helper: score % for a single result
        static double ResultScorePercent(global::QAAutomation.API.Models.EvaluationResult r)
        {
            var max = r.Scores
                .Where(s => s.Field?.FieldType == global::QAAutomation.API.Models.FieldType.Rating)
                .Sum(s => s.Field.MaxRating);
            var total = r.Scores.Sum(s => s.NumericValue ?? 0);
            return max > 0 ? Math.Round(total / max * 100, 1) : 0;
        }

        // ── Decision Drivers ─────────────────────────────────────────────────
        // For each form field/parameter, aggregate scoring patterns across all audits
        var fieldScores = results
            .SelectMany(r => r.Scores
                .Where(s => s.Field != null && s.Field.MaxRating > 0 && s.NumericValue.HasValue)
                .Select(s => new
                {
                    Label = s.Field.Label,
                    Section = s.Field.Section?.Title ?? "",
                    ScorePct = s.NumericValue!.Value / s.Field.MaxRating * 100.0
                }))
            .GroupBy(x => new { x.Label, x.Section })
            .Select(g =>
            {
                var scores = g.Select(x => x.ScorePct).ToList();
                var avg = scores.Average();
                var variance = scores.Count > 1
                    ? scores.Sum(v => Math.Pow(v - avg, 2)) / (scores.Count - 1)
                    : 0;
                return new DecisionDriverDto
                {
                    ParameterLabel = g.Key.Label,
                    SectionTitle = g.Key.Section,
                    AvgScorePercent = Math.Round(avg, 1),
                    LowScoreCount = scores.Count(v => v < 60),
                    HighScoreCount = scores.Count(v => v >= 80),
                    TotalScoredCount = scores.Count,
                    ScoreVariability = Math.Round(Math.Sqrt(variance), 1),
                    IsRiskArea = avg < 60
                };
            })
            .OrderByDescending(d => d.LowScoreCount)
            .ThenBy(d => d.AvgScorePercent)
            .ToList();

        // ── Signal Usage ─────────────────────────────────────────────────────
        var signalUsage = results
            .SelectMany(r => r.Scores
                .Where(s => s.Field != null && s.Field.MaxRating > 0 && s.NumericValue.HasValue)
                .Select(s => new
                {
                    Label = s.Field.Label,
                    Section = s.Field.Section?.Title ?? "",
                    s.NumericValue,
                    MaxRating = s.Field.MaxRating
                }))
            .GroupBy(x => new { x.Label, x.Section })
            .Select(g =>
            {
                var items = g.ToList();
                var fullCount = items.Count(x => x.NumericValue.HasValue && x.NumericValue.Value >= x.MaxRating);
                var missCount = items.Count(x => x.NumericValue.HasValue && x.NumericValue.Value <= 0);
                return new SignalUsageDto
                {
                    ParameterLabel = g.Key.Label,
                    SectionTitle = g.Key.Section,
                    TimesScored = items.Count,
                    TimesFullScore = fullCount,
                    TimesMissed = missCount,
                    FullScoreRate = items.Count > 0 ? Math.Round(fullCount * 100.0 / items.Count, 1) : 0,
                    MissRate = items.Count > 0 ? Math.Round(missCount * 100.0 / items.Count, 1) : 0
                };
            })
            .OrderByDescending(s => s.MissRate)
            .ThenBy(s => s.FullScoreRate)
            .ToList();

        // ── HITL Agreement ───────────────────────────────────────────────────
        var totalReviewed = reviewItems.Count;
        var hitlAgreement = reviewItems
            .GroupBy(h => new
            {
                Verdict = h.ReviewVerdict ?? "Unknown",
                Policy = h.SamplingPolicy?.Name ?? "Manual"
            })
            .Select(g => new HitlAgreementDto
            {
                ReviewVerdict = g.Key.Verdict,
                PolicyName = g.Key.Policy,
                Count = g.Count(),
                Percentage = totalReviewed > 0 ? Math.Round(g.Count() * 100.0 / totalReviewed, 1) : 0
            })
            .OrderBy(h => h.PolicyName)
            .ThenBy(h => h.ReviewVerdict)
            .ToList();

        var agreeCount = reviewItems.Count(h => h.ReviewVerdict == "Agree");
        var agreementRate = totalReviewed > 0 ? Math.Round(agreeCount * 100.0 / totalReviewed, 1) : 0;

        // ── Failure Reasons ──────────────────────────────────────────────────
        // Identify failed audits (overall score < 60 %) and which parameters drove the failure
        var failedResults = results.Where(r => ResultScorePercent(r) < 60).ToList();
        var failedCount = failedResults.Count;

        var failureReasons = failedResults
            .SelectMany(r => r.Scores
                .Where(s => s.Field != null && s.Field.MaxRating > 0 && s.NumericValue.HasValue &&
                            s.NumericValue.Value / s.Field.MaxRating * 100.0 < 60)
                .Select(s => new
                {
                    Label = s.Field.Label,
                    Section = s.Field.Section?.Title ?? "",
                    ScorePct = s.NumericValue!.Value / s.Field.MaxRating * 100.0
                }))
            .GroupBy(x => new { x.Label, x.Section })
            .Select(g => new FailureReasonDto
            {
                ParameterLabel = g.Key.Label,
                SectionTitle = g.Key.Section,
                FailedAuditCount = g.Count(),
                ContributionPercent = failedCount > 0 ? Math.Round(g.Count() * 100.0 / failedCount, 1) : 0,
                AvgScoreInFailedAudits = Math.Round(g.Average(x => x.ScorePct), 1)
            })
            .OrderByDescending(f => f.FailedAuditCount)
            .Take(15)
            .ToList();

        return Ok(new ExplainabilityDto
        {
            TotalAudits = results.Count,
            TotalReviewed = totalReviewed,
            AiHitlAgreementRate = agreementRate,
            DecisionDrivers = fieldScores,
            SignalUsage = signalUsage,
            HitlAgreement = hitlAgreement,
            FailureReasons = failureReasons
        });
    }
}
