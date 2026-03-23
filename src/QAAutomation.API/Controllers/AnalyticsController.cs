using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;
using QAAutomation.API.Data;
using QAAutomation.API.DTOs;
using QAAutomation.API.Services;

namespace QAAutomation.API.Controllers;

/// <summary>Returns pre-aggregated analytics data for the QA dashboard.</summary>
[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAiConfigService _aiConfig;

    public AnalyticsController(AppDbContext db, IAiConfigService aiConfig)
    {
        _db = db;
        _aiConfig = aiConfig;
    }

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

        // ── Agent daily trend (per-agent per-date scores) ─────────────────────
        var agentDailyTrends = results
            .Where(r => !string.IsNullOrWhiteSpace(r.AgentName))
            .GroupBy(r => new { Agent = r.AgentName!, Date = (r.CallDate ?? r.EvaluatedAt).Date })
            .OrderBy(g => g.Key.Date).ThenBy(g => g.Key.Agent)
            .Select(g => new AgentDailyTrendDto
            {
                AgentName = g.Key.Agent,
                Date = g.Key.Date.ToString("yyyy-MM-dd"),
                AvgScorePercent = Math.Round(g.Average(ScorePercent), 1),
                AuditCount = g.Count()
            }).ToList();

        // ── Section daily trend (per-section per-date averages) ───────────────
        var sectionDailyTrends = results
            .SelectMany(r => r.Scores
                .Where(s => s.Field != null && s.Field.MaxRating > 0 && s.NumericValue.HasValue && s.Field.Section != null)
                .Select(s => new
                {
                    Section = s.Field.Section!.Title,
                    Date = (r.CallDate ?? r.EvaluatedAt).Date,
                    ScorePct = Math.Round(s.NumericValue!.Value / s.Field.MaxRating * 100, 1)
                }))
            .GroupBy(x => new { x.Section, x.Date })
            .OrderBy(g => g.Key.Date).ThenBy(g => g.Key.Section)
            .Select(g => new SectionDailyTrendDto
            {
                SectionTitle = g.Key.Section,
                Date = g.Key.Date.ToString("yyyy-MM-dd"),
                AvgScorePercent = Math.Round(g.Average(x => x.ScorePct), 1),
                ScoredCount = g.Count()
            }).ToList();

        return Ok(new AnalyticsDto
        {
            TotalAudits = results.Count,
            DailyScores = daily,
            AgentScores = agents,
            ParameterTrends = paramTrends,
            CallTypeScores = callTypes,
            AgentDailyTrends = agentDailyTrends,
            SectionDailyTrends = sectionDailyTrends
        });
    }

    // ── GET /api/analytics/insights ───────────────────────────────────────────

    /// <summary>
    /// Uses the configured LLM to generate natural-language insights for each section of
    /// the main Analytics Dashboard (daily trend, agent performance, parameters, call types).
    /// Returns an empty DTO (all nulls) when the LLM is not configured or data is insufficient.
    /// </summary>
    [HttpGet("insights")]
    [ProducesResponseType(typeof(AnalyticsInsightsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AnalyticsInsightsDto>> GetAnalyticsInsights(
        [FromQuery] int? projectId = null,
        CancellationToken ct = default)
    {
        var cfg = await _aiConfig.GetAsync();
        if (string.IsNullOrWhiteSpace(cfg.LlmEndpoint) || string.IsNullOrWhiteSpace(cfg.LlmApiKey))
            return Ok(new AnalyticsInsightsDto());

        // Load the same data as Get()
        var query = _db.EvaluationResults
            .Include(r => r.Form).ThenInclude(f => f.Lob)
            .Include(r => r.Scores).ThenInclude(s => s.Field).ThenInclude(f => f.Section)
            .AsNoTracking().AsQueryable();
        if (projectId.HasValue)
            query = query.Where(r => r.Form.Lob != null && r.Form.Lob.ProjectId == projectId.Value);
        var results = await query.ToListAsync(ct);

        if (results.Count == 0)
            return Ok(new AnalyticsInsightsDto());

        static double Pct(global::QAAutomation.API.Models.EvaluationResult r)
        {
            var max = r.Scores.Where(s => s.Field?.FieldType == global::QAAutomation.API.Models.FieldType.Rating).Sum(s => s.Field.MaxRating);
            var total = r.Scores.Sum(s => s.NumericValue ?? 0);
            return max > 0 ? Math.Round(total / max * 100, 1) : 0;
        }

        // Daily trend summary
        var daily = results
            .GroupBy(r => (r.CallDate ?? r.EvaluatedAt).Date)
            .OrderBy(g => g.Key)
            .Select(g => new { Date = g.Key.ToString("yyyy-MM-dd"), Avg = Math.Round(g.Average(Pct), 1), Count = g.Count() })
            .ToList();
        var dailySummary = daily.Count > 0
            ? $"Total {results.Count} audits over {daily.Count} days. " +
              $"Start avg: {daily.First().Avg}%, End avg: {daily.Last().Avg}%. " +
              $"Best day: {daily.MaxBy(d => d.Avg)?.Date} ({daily.Max(d => d.Avg)}%), " +
              $"Worst day: {daily.MinBy(d => d.Avg)?.Date} ({daily.Min(d => d.Avg)}%)."
            : "No daily data.";

        // Agent performance summary (top 5)
        var agents = results
            .Where(r => !string.IsNullOrWhiteSpace(r.AgentName))
            .GroupBy(r => r.AgentName!)
            .Select(g => new { Agent = g.Key, Avg = Math.Round(g.Average(Pct), 1), Count = g.Count() })
            .OrderByDescending(a => a.Avg).Take(5)
            .Select(a => $"{a.Agent}: {a.Avg}% ({a.Count} audits)");
        var agentSummary = string.Join(", ", agents);

        // Parameter summary (5 lowest)
        var paramSummary = results
            .SelectMany(r => r.Scores
                .Where(s => s.Field != null && s.Field.MaxRating > 0 && s.NumericValue.HasValue)
                .Select(s => new { Label = s.Field.Label, ScorePct = s.NumericValue!.Value / s.Field.MaxRating * 100.0 }))
            .GroupBy(x => x.Label)
            .Select(g => new { Label = g.Key, Avg = Math.Round(g.Average(x => x.ScorePct), 1) })
            .OrderBy(p => p.Avg).Take(5)
            .Select(p => $"{p.Label}: {p.Avg}%");

        // Call-type summary
        var ctSummary = results
            .GroupBy(r => r.Form?.Name ?? "Unknown")
            .Select(g => new { Form = g.Key, Avg = Math.Round(g.Average(Pct), 1), Count = g.Count() })
            .OrderByDescending(c => c.Count).Take(5)
            .Select(c => $"{c.Form}: {c.Avg}% ({c.Count} audits)");

        // Generate AI insights
        var (ep, dep) = AzureOpenAIHelper.NormalizeEndpoint(cfg.LlmEndpoint, cfg.LlmDeployment);
        var aiClient = AzureOpenAIHelper.CreateClient(ep, cfg.LlmApiKey, dep);
        var aiOpts = new ChatCompletionOptions { Temperature = 0.4f, MaxOutputTokenCount = 180 };

        async Task<string?> Ask(string prompt)
        {
            try
            {
                var messages = new List<ChatMessage>
                {
                    ChatMessage.CreateSystemMessage(
                        "You are a QA analytics expert. Write concise, actionable insights (2-3 sentences, plain English) " +
                        "based on the data summary provided. Focus on what the numbers mean for call-centre quality and what action to take."),
                    ChatMessage.CreateUserMessage(prompt)
                };
                var resp = await aiClient.CompleteChatAsync(messages, aiOpts, ct);
                return resp.Value.Content[0].Text?.Trim();
            }
            catch { return null; }
        }

        var dailyTask = Ask(
            $"Day-by-day QA score trend: {dailySummary}\n" +
            "What does this trend indicate about quality improvement or decline?");

        var agentTask = string.IsNullOrWhiteSpace(agentSummary)
            ? Task.FromResult<string?>(null)
            : Ask($"Agent performance ({results.Count} total audits). Top agents by avg score:\n{agentSummary}\n" +
                  "What does this distribution indicate about agent performance and coaching needs?");

        var paramTask = Ask(
            $"Parameter performance — 5 lowest-scoring parameters across {results.Count} audits:\n" +
            string.Join("\n", paramSummary) +
            "\nWhat do these low scores indicate and what should be prioritised?");

        var ctTask = Ask(
            $"Call type / form performance ({results.Count} total audits):\n" +
            string.Join("\n", ctSummary) +
            "\nWhat does this distribution indicate about which call types need quality focus?");

        await Task.WhenAll(dailyTask, agentTask, paramTask, ctTask);

        return Ok(new AnalyticsInsightsDto
        {
            DailyTrendInsight       = await dailyTask,
            AgentPerformanceInsight = await agentTask,
            ParameterInsight        = await paramTask,
            CallTypeInsight         = await ctTask
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

    // ── GET /api/analytics/explainability/insights ────────────────────────────

    /// <summary>
    /// Uses the configured LLM to generate natural-language insights for each section of
    /// the Explainability page (Decision Drivers, HITL Agreement, Signal Usage, Failure Reasons).
    /// Returns an empty DTO (all nulls) when the LLM is not configured or data is insufficient.
    /// </summary>
    [HttpGet("explainability/insights")]
    [ProducesResponseType(typeof(ExplainabilityInsightsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExplainabilityInsightsDto>> GetExplainabilityInsights(
        [FromQuery] int? projectId = null,
        CancellationToken ct = default)
    {
        var cfg = await _aiConfig.GetAsync();
        if (string.IsNullOrWhiteSpace(cfg.LlmEndpoint) || string.IsNullOrWhiteSpace(cfg.LlmApiKey))
            return Ok(new ExplainabilityInsightsDto());

        // ── Load data (same scope as GetExplainability) ──────────────────────
        var resultQuery = _db.EvaluationResults
            .Include(r => r.Form).ThenInclude(f => f.Lob)
            .Include(r => r.Scores).ThenInclude(s => s.Field).ThenInclude(f => f.Section)
            .AsNoTracking().AsQueryable();
        if (projectId.HasValue)
            resultQuery = resultQuery.Where(r => r.Form.Lob != null && r.Form.Lob.ProjectId == projectId.Value);
        var results = await resultQuery.ToListAsync(ct);

        var reviewQuery = _db.HumanReviewItems
            .Include(h => h.SamplingPolicy)
            .Include(h => h.EvaluationResult).ThenInclude(e => e!.Form).ThenInclude(f => f.Lob)
            .Where(h => h.Status == "Reviewed" && h.ReviewVerdict != null)
            .AsNoTracking().AsQueryable();
        if (projectId.HasValue)
            reviewQuery = reviewQuery.Where(h =>
                h.EvaluationResult != null &&
                h.EvaluationResult.Form.Lob != null &&
                h.EvaluationResult.Form.Lob.ProjectId == projectId.Value);
        var reviewItems = await reviewQuery.ToListAsync(ct);

        if (results.Count == 0)
            return Ok(new ExplainabilityInsightsDto());

        // ── Pre-compute summaries to keep prompts concise ─────────────────────
        static double Pct(global::QAAutomation.API.Models.EvaluationResult r)
        {
            var max = r.Scores.Where(s => s.Field?.FieldType == global::QAAutomation.API.Models.FieldType.Rating).Sum(s => s.Field.MaxRating);
            var total = r.Scores.Sum(s => s.NumericValue ?? 0);
            return max > 0 ? Math.Round(total / max * 100, 1) : 0;
        }

        // Decision drivers — top 6 by low-score count
        var ddSummary = results
            .SelectMany(r => r.Scores
                .Where(s => s.Field != null && s.Field.MaxRating > 0 && s.NumericValue.HasValue)
                .Select(s => new { Label = s.Field.Label, ScorePct = s.NumericValue!.Value / s.Field.MaxRating * 100.0 }))
            .GroupBy(x => x.Label)
            .Select(g => new { Label = g.Key, Avg = Math.Round(g.Average(x => x.ScorePct), 1), Low = g.Count(x => x.ScorePct < 60) })
            .OrderByDescending(x => x.Low).Take(6)
            .Select(x => $"{x.Label}: avg {x.Avg}%, low-score count {x.Low}");

        // Signal usage — top 5 by miss rate
        var suSummary = results
            .SelectMany(r => r.Scores
                .Where(s => s.Field != null && s.Field.MaxRating > 0 && s.NumericValue.HasValue)
                .Select(s => new { Label = s.Field.Label, Missed = s.NumericValue!.Value <= 0, Full = s.NumericValue.Value >= s.Field.MaxRating }))
            .GroupBy(x => x.Label)
            .Select(g => new { Label = g.Key, MissRate = Math.Round(g.Count(x => x.Missed) * 100.0 / g.Count(), 1), FullRate = Math.Round(g.Count(x => x.Full) * 100.0 / g.Count(), 1) })
            .OrderByDescending(x => x.MissRate).Take(5)
            .Select(x => $"{x.Label}: full-score {x.FullRate}%, missed {x.MissRate}%");

        // HITL agreement summary
        var totalReviewed = reviewItems.Count;
        var agreeRate = totalReviewed > 0 ? Math.Round(reviewItems.Count(h => h.ReviewVerdict == "Agree") * 100.0 / totalReviewed, 1) : 0;
        var hitlSummary = reviewItems
            .GroupBy(h => h.ReviewVerdict ?? "Unknown")
            .Select(g => $"{g.Key}: {g.Count()} ({Math.Round(g.Count() * 100.0 / Math.Max(1, totalReviewed), 1)}%)")
            .ToList();

        // Failure reasons — top 5
        var failedResults = results.Where(r => Pct(r) < 60).ToList();
        var frSummary = failedResults
            .SelectMany(r => r.Scores
                .Where(s => s.Field != null && s.Field.MaxRating > 0 && s.NumericValue.HasValue && s.NumericValue.Value / s.Field.MaxRating * 100.0 < 60)
                .Select(s => s.Field.Label))
            .GroupBy(l => l)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(5)
            .Select(x => $"{x.Label}: appeared in {x.Count} of {failedResults.Count} failed audits");

        // ── Generate all four insights in parallel ────────────────────────────
        var (ep, dep) = AzureOpenAIHelper.NormalizeEndpoint(cfg.LlmEndpoint, cfg.LlmDeployment);
        var aiClient = AzureOpenAIHelper.CreateClient(ep, cfg.LlmApiKey, dep);
        var aiOpts = new ChatCompletionOptions { Temperature = 0.4f, MaxOutputTokenCount = 180 };

        async Task<string?> Ask(string prompt)
        {
            try
            {
                var messages = new List<ChatMessage>
                {
                    ChatMessage.CreateSystemMessage(
                        "You are a QA analytics expert. Write concise, actionable insights (2-3 sentences, plain English) " +
                        "based on the data summary provided. Focus on what the numbers mean for call-centre quality and what action to take."),
                    ChatMessage.CreateUserMessage(prompt)
                };
                var resp = await aiClient.CompleteChatAsync(messages, aiOpts, ct);
                return resp.Value.Content[0].Text?.Trim();
            }
            catch { return null; }
        }

        var ddTask = Ask(
            $"Decision Drivers (top parameters by low-score count across {results.Count} audits):\n" +
            string.Join("\n", ddSummary) +
            "\n\nWhat does this tell us about which areas need the most quality improvement?");

        var hitlTask = totalReviewed > 0
            ? Ask($"AI–Human Agreement Rate: {agreeRate}% across {totalReviewed} human reviews.\n" +
                  "Verdict breakdown: " + string.Join(", ", hitlSummary) +
                  "\n\nWhat does this agreement level indicate about AI decision quality and trust?")
            : Task.FromResult<string?>(null);

        var suTask = Ask(
            $"Signal Utilisation (top parameters by miss rate across {results.Count} audits):\n" +
            string.Join("\n", suSummary) +
            "\n\nWhat do high miss rates indicate and what actions should the team take?");

        var frTask = failedResults.Count > 0
            ? Ask($"Failure Reason Analysis ({failedResults.Count} failed audits out of {results.Count} total):\n" +
                  string.Join("\n", frSummary) +
                  "\n\nWhich parameters are the biggest drivers of audit failures and what should be prioritised?")
            : Task.FromResult<string?>(null);

        await Task.WhenAll(ddTask, hitlTask, suTask, frTask);

        return Ok(new ExplainabilityInsightsDto
        {
            DecisionDriversInsight = await ddTask,
            HitlAgreementInsight   = await hitlTask,
            SignalUsageInsight      = await suTask,
            FailureReasonsInsight   = await frTask
        });
    }
}
