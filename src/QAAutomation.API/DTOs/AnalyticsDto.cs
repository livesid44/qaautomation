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
