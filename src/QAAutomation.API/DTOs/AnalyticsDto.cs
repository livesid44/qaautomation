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
