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

        // ── Section scores (overall avg per section) ──────────────────────────────
        var sectionScores = results
            .SelectMany(r => r.Scores
                .Where(s => s.Field != null && s.Field.MaxRating > 0 && s.NumericValue.HasValue && s.Field.Section != null)
                .Select(s => new
                {
                    Section = s.Field.Section!.Title,
                    ResultId = r.Id,
                    ScorePct = Math.Round(s.NumericValue!.Value / s.Field.MaxRating * 100, 1),
                    ParameterLabel = s.Field.Label
                }))
            .GroupBy(x => x.Section)
            .Select(g => new SectionScoreDto
            {
                SectionTitle = g.Key,
                AvgScorePercent = Math.Round(g.Average(x => x.ScorePct), 1),
                AuditCount = g.Select(x => x.ResultId).Distinct().Count(),
                ParameterCount = g.Select(x => x.ParameterLabel).Distinct().Count()
            })
            .OrderBy(s => s.SectionTitle)
            .ToList();

        return Ok(new AnalyticsDto
        {
            TotalAudits = results.Count,
            DailyScores = daily,
            AgentScores = agents,
            ParameterTrends = paramTrends,
            CallTypeScores = callTypes,
            AgentDailyTrends = agentDailyTrends,
            SectionDailyTrends = sectionDailyTrends,
            SectionScores = sectionScores
        });
    }

    // ── GET /api/analytics/tni ────────────────────────────────────────────────

    /// <summary>Returns TNI (Training Needs Identification) summary statistics for the dashboard.</summary>
    [HttpGet("tni")]
    [ProducesResponseType(typeof(TniSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TniSummaryDto>> GetTniSummary([FromQuery] int? projectId = null)
    {
        var query = _db.TrainingPlans.Include(p => p.Items).AsNoTracking().AsQueryable();
        if (projectId.HasValue)
            query = query.Where(p => p.ProjectId == projectId.Value);

        var plans = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();

        var today = DateTime.UtcNow.Date;
        var openStatuses = new HashSet<string> { "Draft", "Active", "InProgress" };
        var closedStatuses = new HashSet<string> { "Completed", "Closed" };

        var byStatus = plans
            .GroupBy(p => p.Status)
            .Select(g => new TniStatusCountDto { Status = g.Key, Count = g.Count() })
            .OrderBy(s => s.Status)
            .ToList();

        var byAgent = plans
            .Where(p => !string.IsNullOrWhiteSpace(p.AgentName))
            .GroupBy(p => p.AgentName)
            .Select(g =>
            {
                var open = g.Count(p => openStatuses.Contains(p.Status));
                var done = g.Count(p => closedStatuses.Contains(p.Status));
                return new TniAgentSummaryDto
                {
                    AgentName = g.Key,
                    TotalPlans = g.Count(),
                    OpenPlans = open,
                    CompletedPlans = done,
                    CompletionRate = g.Count() > 0 ? Math.Round(done * 100.0 / g.Count(), 1) : 0
                };
            })
            .OrderByDescending(a => a.OpenPlans)
            .ToList();

        var recent = plans.Take(10).Select(p => new TniRecentPlanDto
        {
            Id = p.Id,
            Title = p.Title,
            AgentName = p.AgentName,
            Status = p.Status,
            CreatedAt = p.CreatedAt,
            DueDate = p.DueDate,
            TotalItems = p.Items.Count,
            CompletedItems = p.Items.Count(i => i.Status == "Done"),
            IsAutoGenerated = p.IsAutoGenerated
        }).ToList();

        return Ok(new TniSummaryDto
        {
            TotalPlans = plans.Count,
            OpenPlans = plans.Count(p => openStatuses.Contains(p.Status)),
            CompletedPlans = plans.Count(p => closedStatuses.Contains(p.Status)),
            OverduePlans = plans.Count(p => p.DueDate.HasValue && p.DueDate.Value.Date < today && openStatuses.Contains(p.Status)),
            ByStatus = byStatus,
            ByAgent = byAgent,
            RecentPlans = recent
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

    // ── GET /api/analytics/decision-assurance ─────────────────────────────────

    /// <summary>
    /// Advanced analytics: Decision Confidence Scores, Agent Risk Profiles, Section
    /// Calibration, Risk Radar items, and the Calibration Heatmap — all derived from
    /// existing audit data with no additional schema changes.
    /// Pass ?projectId=N to restrict analysis to a single project.
    /// </summary>
    [HttpGet("decision-assurance")]
    [ProducesResponseType(typeof(DecisionAssuranceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DecisionAssuranceDto>> GetDecisionAssurance([FromQuery] int? projectId = null)
    {
        var query = _db.EvaluationResults
            .Include(r => r.Form).ThenInclude(f => f.Lob)
            .Include(r => r.Scores).ThenInclude(s => s.Field).ThenInclude(f => f.Section)
            .AsNoTracking()
            .AsQueryable();

        if (projectId.HasValue)
            query = query.Where(r => r.Form.Lob != null && r.Form.Lob.ProjectId == projectId.Value);

        var results = await query.ToListAsync();

        if (results.Count == 0)
            return Ok(new DecisionAssuranceDto());

        // ── Helpers ───────────────────────────────────────────────────────────
        static double ScorePct(global::QAAutomation.API.Models.EvaluationResult r)
        {
            var max   = r.Scores.Where(s => s.Field?.FieldType == global::QAAutomation.API.Models.FieldType.Rating).Sum(s => s.Field.MaxRating);
            var total = r.Scores.Sum(s => s.NumericValue ?? 0);
            return max > 0 ? Math.Round(total / max * 100, 1) : 0;
        }

        static double StdDev(IEnumerable<double> values)
        {
            var list = values.ToList();
            if (list.Count < 2) return 0;
            var mean = list.Average();
            return Math.Sqrt(list.Average(v => Math.Pow(v - mean, 2)));
        }

        static string RiskLevel(double score) =>
            score >= 75 ? "Low" : score >= 50 ? "Medium" : "High";

        static string TrendLabel(double momentum) =>
            momentum >= 3 ? "Improving" : momentum <= -3 ? "Declining" : "Stable";

        var now = DateTime.UtcNow;
        var cutRecent = now.AddDays(-30);
        var cutPrior  = now.AddDays(-60);

        // ── 1. Decision Confidence Scores (per parameter) ─────────────────────
        var paramScores = results
            .SelectMany(r => r.Scores
                .Where(s => s.Field != null && s.Field.MaxRating > 0 && s.NumericValue.HasValue)
                .Select(s => new
                {
                    Label   = s.Field.Label,
                    Section = s.Field.Section?.Title ?? "",
                    Pct     = Math.Round(s.NumericValue!.Value / s.Field.MaxRating * 100, 1)
                }))
            .GroupBy(x => new { x.Label, x.Section })
            .Select(g =>
            {
                var pcts       = g.Select(x => x.Pct).ToList();
                var avg        = Math.Round(pcts.Average(), 1);
                var stdDev     = Math.Round(StdDev(pcts), 1);
                var consistency= Math.Round(Math.Max(0, 1 - stdDev / 100), 3);
                var confidence = Math.Round(avg * consistency, 1);
                return new DecisionConfidenceDto
                {
                    ParameterLabel  = g.Key.Label,
                    SectionTitle    = g.Key.Section,
                    AvgScorePercent = avg,
                    ScoreStdDev     = stdDev,
                    ConsistencyScore= consistency,
                    ConfidenceScore = confidence,
                    RiskLevel       = RiskLevel(confidence),
                    AuditCount      = g.Count()
                };
            })
            .OrderBy(p => p.ConfidenceScore)
            .ToList();

        // ── 2. Agent Risk Profiles ─────────────────────────────────────────────
        var agentProfiles = results
            .Where(r => !string.IsNullOrWhiteSpace(r.AgentName))
            .GroupBy(r => r.AgentName!)
            .Select(g =>
            {
                var allPcts    = g.Select(ScorePct).ToList();
                var avg        = Math.Round(allPcts.Average(), 1);

                var recentPcts = g.Where(r => (r.CallDate ?? r.EvaluatedAt) >= cutRecent)
                                  .Select(ScorePct).ToList();
                var priorPcts  = g.Where(r =>
                    {
                        var d = r.CallDate ?? r.EvaluatedAt;
                        return d >= cutPrior && d < cutRecent;
                    })
                                  .Select(ScorePct).ToList();

                var recentAvg = recentPcts.Count > 0 ? Math.Round(recentPcts.Average(), 1) : avg;
                var priorAvg  = priorPcts.Count  > 0 ? Math.Round(priorPcts.Average(),  1) : avg;
                var momentum  = Math.Round(recentAvg - priorAvg, 1);

                // Risk: declining trend or low overall score
                var riskScore = avg - Math.Min(0, momentum * 3);
                return new AgentRiskProfileDto
                {
                    AgentName       = g.Key,
                    AuditCount      = g.Count(),
                    AvgScorePercent = avg,
                    RecentAvgPercent= recentAvg,
                    PriorAvgPercent = priorAvg,
                    Momentum        = momentum,
                    Trend           = TrendLabel(momentum),
                    RiskLevel       = RiskLevel(riskScore)
                };
            })
            .OrderBy(a => a.AvgScorePercent)
            .ToList();

        // ── 3. Section Calibration ─────────────────────────────────────────────
        var sectionCalibration = results
            .SelectMany(r => r.Scores
                .Where(s => s.Field?.Section != null && s.Field.MaxRating > 0 && s.NumericValue.HasValue)
                .Select(s => new
                {
                    Section   = s.Field.Section!.Title,
                    Pct       = Math.Round(s.NumericValue!.Value / s.Field.MaxRating * 100, 1),
                    Date      = r.CallDate ?? r.EvaluatedAt
                }))
            .GroupBy(x => x.Section)
            .Select(g =>
            {
                var all        = g.Select(x => x.Pct).ToList();
                var avg        = Math.Round(all.Average(), 1);
                var stdDev     = Math.Round(StdDev(all), 1);
                var confusion  = Math.Round(avg > 0 ? stdDev / avg * 100 : 0, 1);

                var recentAvg  = g.Where(x => x.Date >= cutRecent).Select(x => x.Pct).ToList()
                                  is { Count: > 0 } rp ? Math.Round(rp.Average(), 1) : avg;
                var priorAvg   = g.Where(x => x.Date >= cutPrior && x.Date < cutRecent).Select(x => x.Pct).ToList()
                                  is { Count: > 0 } pp ? Math.Round(pp.Average(), 1) : avg;

                var drift = recentAvg - priorAvg;
                var driftLabel = stdDev > 20 ? "Volatile"
                               : drift >=  3 ? "Improving"
                               : drift <= -3 ? "Declining" : "Stable";

                return new SectionCalibrationDto
                {
                    SectionTitle      = g.Key,
                    OverallAvgPercent = avg,
                    RecentAvgPercent  = recentAvg,
                    PriorAvgPercent   = priorAvg,
                    ScoreStdDev       = stdDev,
                    ConfusionScore    = Math.Min(confusion, 100),
                    DriftDirection    = driftLabel,
                    AuditCount        = all.Count
                };
            })
            .OrderByDescending(s => s.ConfusionScore)
            .ToList();

        // ── 4. Risk Radar ─────────────────────────────────────────────────────
        var riskRadar = new List<RiskRadarItemDto>();

        // PolicyConfusion: parameters with stddev > 25 (high inconsistency)
        foreach (var p in paramScores.Where(p => p.ScoreStdDev > 25).Take(5))
        {
            riskRadar.Add(new RiskRadarItemDto
            {
                ParameterLabel = p.ParameterLabel,
                SectionTitle   = p.SectionTitle,
                RiskCategory   = "PolicyConfusion",
                RiskScore      = Math.Min(100, p.ScoreStdDev * 2),
                Description    = $"High score variability (StdDev={p.ScoreStdDev}%) suggests inconsistent policy interpretation."
            });
        }

        // EscalationRisk: parameters with avg < 50 and audit count > 3
        foreach (var p in paramScores.Where(p => p.AvgScorePercent < 50 && p.AuditCount >= 3).Take(5))
        {
            riskRadar.Add(new RiskRadarItemDto
            {
                ParameterLabel = p.ParameterLabel,
                SectionTitle   = p.SectionTitle,
                RiskCategory   = "EscalationRisk",
                RiskScore      = Math.Round(100 - p.AvgScorePercent, 1),
                Description    = $"Consistently low scores ({p.AvgScorePercent}%) may drive escalations or appeals."
            });
        }

        // DecisionReversal: declining agent momentum
        foreach (var a in agentProfiles.Where(a => a.Momentum <= -5).Take(5))
        {
            riskRadar.Add(new RiskRadarItemDto
            {
                ParameterLabel = a.AgentName,
                SectionTitle   = "Agent",
                RiskCategory   = "DecisionReversal",
                RiskScore      = Math.Min(100, Math.Abs(a.Momentum) * 5),
                Description    = $"{a.AgentName} score dropped {Math.Abs(a.Momentum)}% recently — risk of reversed decisions."
            });
        }

        // BiasIndicator: sections with volatile drift
        foreach (var s in sectionCalibration.Where(s => s.DriftDirection == "Volatile").Take(3))
        {
            riskRadar.Add(new RiskRadarItemDto
            {
                ParameterLabel = s.SectionTitle,
                SectionTitle   = "Section",
                RiskCategory   = "BiasIndicator",
                RiskScore      = Math.Min(100, s.ConfusionScore),
                Description    = $"Section '{s.SectionTitle}' shows volatile scoring (confusion={s.ConfusionScore}%) — possible evaluator bias."
            });
        }

        riskRadar = riskRadar.OrderByDescending(r => r.RiskScore).ToList();

        // ── 5. Calibration Heatmap ────────────────────────────────────────────
        // Per-parameter per-agent avg score → reveals disagreement between evaluators
        var agentNames = results
            .Where(r => !string.IsNullOrWhiteSpace(r.AgentName))
            .Select(r => r.AgentName!)
            .Distinct().OrderBy(a => a).Take(8)
            .ToList();

        var heatmap = results
            .SelectMany(r => r.Scores
                .Where(s => s.Field != null && s.Field.MaxRating > 0 && s.NumericValue.HasValue
                         && !string.IsNullOrWhiteSpace(r.AgentName) && agentNames.Contains(r.AgentName))
                .Select(s => new
                {
                    Agent   = r.AgentName!,
                    Label   = s.Field.Label,
                    Section = s.Field.Section?.Title ?? "",
                    Pct     = Math.Round(s.NumericValue!.Value / s.Field.MaxRating * 100, 1)
                }))
            .GroupBy(x => new { x.Label, x.Section })
            .Where(g => g.Select(x => x.Agent).Distinct().Count() >= 2) // only params scored by ≥2 agents
            .Select(g =>
            {
                var byAgent = agentNames.ToDictionary(
                    a => a,
                    a =>
                    {
                        var pcts = g.Where(x => x.Agent == a).Select(x => x.Pct).ToList();
                        return pcts.Count > 0 ? Math.Round(pcts.Average(), 1) : (double?)null;
                    })
                    .Where(kv => kv.Value.HasValue)
                    .ToDictionary(kv => kv.Key, kv => kv.Value!.Value);

                var spread = byAgent.Count >= 2 ? byAgent.Values.Max() - byAgent.Values.Min() : 0;
                return new CalibrationHeatmapRowDto
                {
                    ParameterLabel = g.Key.Label,
                    SectionTitle   = g.Key.Section,
                    AgentAvgScores = byAgent,
                    AgentSpread    = Math.Round(spread, 1)
                };
            })
            .OrderByDescending(h => h.AgentSpread)
            .Take(20)
            .ToList();

        return Ok(new DecisionAssuranceDto
        {
            TotalAudits          = results.Count,
            DecisionConfidences  = paramScores,
            AgentRiskProfiles    = agentProfiles,
            SectionCalibration   = sectionCalibration,
            RiskRadar            = riskRadar,
            CalibrationHeatmap   = heatmap
        });
    }
}
