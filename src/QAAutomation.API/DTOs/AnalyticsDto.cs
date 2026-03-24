namespace QAAutomation.API.DTOs;

/// <summary>Full analytics payload returned by GET /api/analytics.</summary>
public class AnalyticsDto
{
    /// <summary>Average QA score % per calendar day (based on CallDate, falling back to EvaluatedAt).</summary>
    public List<DailyScoreDto> DailyScores { get; set; } = new();

    /// <summary>Average QA score % per agent name.</summary>
    public List<AgentScoreDto> AgentScores { get; set; } = new();

    /// <summary>Average score % per form field / parameter (top 15 by audit count).</summary>
    public List<ParameterTrendDto> ParameterTrends { get; set; } = new();

    /// <summary>Average QA score % per evaluation form (used as "call type").</summary>
    public List<CallTypeScoreDto> CallTypeScores { get; set; } = new();

    /// <summary>Per-agent daily score trend — one row per agent per date.</summary>
    public List<AgentDailyTrendDto> AgentDailyTrends { get; set; } = new();

    /// <summary>Per-section daily score trend — one row per section per date.</summary>
    public List<SectionDailyTrendDto> SectionDailyTrends { get; set; } = new();

    /// <summary>Overall average score % per form section (aggregated across all audits).</summary>
    public List<SectionScoreDto> SectionScores { get; set; } = new();

    /// <summary>Total number of audit records included in this analysis.</summary>
    public int TotalAudits { get; set; }
}

public class DailyScoreDto
{
    public string Date { get; set; } = string.Empty;   // "yyyy-MM-dd"
    public double AvgScorePercent { get; set; }
    public int AuditCount { get; set; }
}

public class AgentScoreDto
{
    public string AgentName { get; set; } = string.Empty;
    public double AvgScorePercent { get; set; }
    public int AuditCount { get; set; }
}

public class ParameterTrendDto
{
    public string ParameterLabel { get; set; } = string.Empty;
    public string SectionTitle { get; set; } = string.Empty;
    public double AvgScorePercent { get; set; }
    public int ScoredCount { get; set; }
}

public class CallTypeScoreDto
{
    public string FormName { get; set; } = string.Empty;
    public double AvgScorePercent { get; set; }
    public int AuditCount { get; set; }
}

/// <summary>One agent's average QA score for a single calendar day.</summary>
public class AgentDailyTrendDto
{
    public string AgentName { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;   // "yyyy-MM-dd"
    public double AvgScorePercent { get; set; }
    public int AuditCount { get; set; }
}

/// <summary>One section's average QA score for a single calendar day.</summary>
public class SectionDailyTrendDto
{
    public string SectionTitle { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;   // "yyyy-MM-dd"
    public double AvgScorePercent { get; set; }
    public int ScoredCount { get; set; }
}

/// <summary>
/// AI-generated natural-language insights for the main analytics dashboard.
/// Fields are null when the LLM is not configured or there is insufficient data.
/// </summary>
public class AnalyticsInsightsDto
{
    /// <summary>Insight about the day-by-day QA score trend.</summary>
    public string? DailyTrendInsight { get; set; }

    /// <summary>Insight about agent-level performance patterns.</summary>
    public string? AgentPerformanceInsight { get; set; }

    /// <summary>Insight about parameter and section-level performance.</summary>
    public string? ParameterInsight { get; set; }

    /// <summary>Insight about call-type / form performance distribution.</summary>
    public string? CallTypeInsight { get; set; }
}

// ── Explainability analytics DTOs ─────────────────────────────────────────────

/// <summary>Full explainability payload returned by GET /api/analytics/explainability.</summary>
public class ExplainabilityDto
{
    public int TotalAudits { get; set; }
    public int TotalReviewed { get; set; }
    public double AiHitlAgreementRate { get; set; }

    /// <summary>Per-parameter decision drivers: shows which signals drove pass/fail outcomes.</summary>
    public List<DecisionDriverDto> DecisionDrivers { get; set; } = new();

    /// <summary>Signal usage statistics: how consistently each parameter is scored and where it is missed.</summary>
    public List<SignalUsageDto> SignalUsage { get; set; } = new();

    /// <summary>Human-in-the-loop agreement breakdown by verdict and sampling policy.</summary>
    public List<HitlAgreementDto> HitlAgreement { get; set; } = new();

    /// <summary>Which parameters most frequently contributed to audit failures (score &lt; 60%).</summary>
    public List<FailureReasonDto> FailureReasons { get; set; } = new();
}

/// <summary>Shows how a single evaluation parameter drove pass/fail decisions across audits.</summary>
public class DecisionDriverDto
{
    public string ParameterLabel { get; set; } = string.Empty;
    public string SectionTitle { get; set; } = string.Empty;
    public double AvgScorePercent { get; set; }
    /// <summary>Number of audits where this parameter scored below 60 % of its maximum.</summary>
    public int LowScoreCount { get; set; }
    /// <summary>Number of audits where this parameter scored at or above 80 % of its maximum.</summary>
    public int HighScoreCount { get; set; }
    /// <summary>Total number of audits that include this parameter.</summary>
    public int TotalScoredCount { get; set; }
    /// <summary>Standard deviation of scores as a %, indicating variability / impact on outcomes.</summary>
    public double ScoreVariability { get; set; }
    /// <summary>True when this parameter's average falls below 60 % — marks it as a risk area.</summary>
    public bool IsRiskArea { get; set; }
}

/// <summary>Tracks how a parameter's signal is utilised: fully scored, partially, or missed entirely.</summary>
public class SignalUsageDto
{
    public string ParameterLabel { get; set; } = string.Empty;
    public string SectionTitle { get; set; } = string.Empty;
    /// <summary>Total number of audits that include a score for this parameter.</summary>
    public int TimesScored { get; set; }
    /// <summary>Number of audits where the parameter received the maximum possible rating.</summary>
    public int TimesFullScore { get; set; }
    /// <summary>Number of audits where the parameter scored zero.</summary>
    public int TimesMissed { get; set; }
    /// <summary>Percentage of scored instances where full marks were awarded.</summary>
    public double FullScoreRate { get; set; }
    /// <summary>Percentage of scored instances where the parameter was completely missed (0).</summary>
    public double MissRate { get; set; }
}

/// <summary>One row of HITL agreement analytics — how often humans agreed/disagreed with AI by verdict and policy.</summary>
public class HitlAgreementDto
{
    public string ReviewVerdict { get; set; } = string.Empty;    // Agree | Disagree | Partial
    public string PolicyName { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
}

/// <summary>Shows which parameter most often caused an audit to be classified as a failure.</summary>
public class FailureReasonDto
{
    public string ParameterLabel { get; set; } = string.Empty;
    public string SectionTitle { get; set; } = string.Empty;
    /// <summary>Number of failed audits where this parameter scored below 60 %.</summary>
    public int FailedAuditCount { get; set; }
    /// <summary>Percentage of all failed audits where this parameter contributed to the failure.</summary>
    public double ContributionPercent { get; set; }
    public double AvgScoreInFailedAudits { get; set; }
}

/// <summary>
/// AI-generated natural-language insights for each section of the Explainability analytics page.
/// Fields are null when the LLM is not configured or when there is insufficient data.
/// </summary>
public class ExplainabilityInsightsDto
{
    /// <summary>2-3 sentence insight for the Decision Drivers chart.</summary>
    public string? DecisionDriversInsight { get; set; }

    /// <summary>2-3 sentence insight for the AI vs Human Agreement chart.</summary>
    public string? HitlAgreementInsight { get; set; }

    /// <summary>2-3 sentence insight for the Signal Utilisation chart.</summary>
    public string? SignalUsageInsight { get; set; }

    /// <summary>2-3 sentence insight for the Failure Reason Analysis table.</summary>
    public string? FailureReasonsInsight { get; set; }
}

// ── Decision Assurance Analytics DTOs ────────────────────────────────────────

/// <summary>Full payload for the Decision Assurance (advanced analytics) page.</summary>
public class DecisionAssuranceDto
{
    public int TotalAudits { get; set; }

    /// <summary>Decision Confidence Score per evaluation parameter — consistency × quality.</summary>
    public List<DecisionConfidenceDto> DecisionConfidences { get; set; } = new();

    /// <summary>Risk profile per agent: trend direction, momentum, risk level.</summary>
    public List<AgentRiskProfileDto> AgentRiskProfiles { get; set; } = new();

    /// <summary>Section-level calibration: recent vs prior avg, confusion score.</summary>
    public List<SectionCalibrationDto> SectionCalibration { get; set; } = new();

    /// <summary>Risk Radar items: parameters flagged for escalation / policy confusion / bias.</summary>
    public List<RiskRadarItemDto> RiskRadar { get; set; } = new();

    /// <summary>Calibration heatmap rows: per-parameter score variability across agents.</summary>
    public List<CalibrationHeatmapRowDto> CalibrationHeatmap { get; set; } = new();
}

/// <summary>Decision Confidence Score for a single evaluation parameter.</summary>
public class DecisionConfidenceDto
{
    public string ParameterLabel { get; set; } = string.Empty;
    public string SectionTitle { get; set; } = string.Empty;
    /// <summary>Mean score % across all audits.</summary>
    public double AvgScorePercent { get; set; }
    /// <summary>Score standard deviation (higher = less consistent decisions).</summary>
    public double ScoreStdDev { get; set; }
    /// <summary>1 – (StdDev / MaxPossible): 1 = perfectly consistent, 0 = chaotic.</summary>
    public double ConsistencyScore { get; set; }
    /// <summary>Composite score: AvgScorePercent × ConsistencyScore / 100.</summary>
    public double ConfidenceScore { get; set; }
    /// <summary>High / Medium / Low based on ConfidenceScore thresholds.</summary>
    public string RiskLevel { get; set; } = "Low";
    public int AuditCount { get; set; }
}

/// <summary>Risk profile for a single agent based on score trend and momentum.</summary>
public class AgentRiskProfileDto
{
    public string AgentName { get; set; } = string.Empty;
    public int AuditCount { get; set; }
    public double AvgScorePercent { get; set; }
    /// <summary>Average score % over the most recent 30 days.</summary>
    public double RecentAvgPercent { get; set; }
    /// <summary>Average score % in the 30-day window before the recent period.</summary>
    public double PriorAvgPercent { get; set; }
    /// <summary>RecentAvg – PriorAvg. Positive = improving, negative = declining.</summary>
    public double Momentum { get; set; }
    /// <summary>Improving / Stable / Declining.</summary>
    public string Trend { get; set; } = "Stable";
    /// <summary>High / Medium / Low based on score and trend.</summary>
    public string RiskLevel { get; set; } = "Low";
}

/// <summary>Section-level calibration: recent vs prior performance and confusion indicator.</summary>
public class SectionCalibrationDto
{
    public string SectionTitle { get; set; } = string.Empty;
    public double OverallAvgPercent { get; set; }
    /// <summary>Average score % for this section in the last 30 days.</summary>
    public double RecentAvgPercent { get; set; }
    /// <summary>Average score % in the 30-day window before the recent period.</summary>
    public double PriorAvgPercent { get; set; }
    /// <summary>Score standard deviation: higher values indicate policy interpretation inconsistency.</summary>
    public double ScoreStdDev { get; set; }
    /// <summary>Confusion score (0–100): derived from stddev relative to mean; higher = more confusion.</summary>
    public double ConfusionScore { get; set; }
    /// <summary>Drift direction: Improving / Stable / Declining / Volatile.</summary>
    public string DriftDirection { get; set; } = "Stable";
    public int AuditCount { get; set; }
}

/// <summary>A single Risk Radar item highlighting a specific risk signal.</summary>
public class RiskRadarItemDto
{
    public string ParameterLabel { get; set; } = string.Empty;
    public string SectionTitle { get; set; } = string.Empty;
    /// <summary>Category: PolicyConfusion | EscalationRisk | BiasIndicator | DecisionReversal.</summary>
    public string RiskCategory { get; set; } = string.Empty;
    /// <summary>0–100 risk intensity.</summary>
    public double RiskScore { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>One row in the calibration heatmap — parameter score variability per agent.</summary>
public class CalibrationHeatmapRowDto
{
    public string ParameterLabel { get; set; } = string.Empty;
    public string SectionTitle { get; set; } = string.Empty;
    /// <summary>Per-agent avg scores: Dictionary&lt;agentName, avgScore%&gt;.</summary>
    public Dictionary<string, double> AgentAvgScores { get; set; } = new();
    /// <summary>Max – Min across agents: high spread = calibration gap.</summary>
    public double AgentSpread { get; set; }
}

// ── Section Score ─────────────────────────────────────────────────────────────

/// <summary>Aggregated average QA score % for a single form section across all audits.</summary>
public class SectionScoreDto
{
    public string SectionTitle { get; set; } = string.Empty;
    public double AvgScorePercent { get; set; }
    public int AuditCount { get; set; }
    public int ParameterCount { get; set; }
}

// ── TNI Summary ───────────────────────────────────────────────────────────────

/// <summary>High-level TNI (Training Needs Identification) dashboard data.</summary>
public class TniSummaryDto
{
    public int TotalPlans { get; set; }
    public int OpenPlans { get; set; }       // Draft + Active + InProgress
    public int CompletedPlans { get; set; }  // Completed + Closed
    public int OverduePlans { get; set; }    // DueDate < today and not closed/completed
    public List<TniStatusCountDto> ByStatus { get; set; } = new();
    public List<TniAgentSummaryDto> ByAgent { get; set; } = new();
    public List<TniRecentPlanDto> RecentPlans { get; set; } = new();
}

public class TniStatusCountDto
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class TniAgentSummaryDto
{
    public string AgentName { get; set; } = string.Empty;
    public int TotalPlans { get; set; }
    public int OpenPlans { get; set; }
    public int CompletedPlans { get; set; }
    public double CompletionRate { get; set; }
}

public class TniRecentPlanDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public int TotalItems { get; set; }
    public int CompletedItems { get; set; }
    public bool IsAutoGenerated { get; set; }
}
