namespace QAAutomation.API.DTOs;

public class EvaluationResultDto
{
    public int Id { get; set; }
    public int FormId { get; set; }
    public string FormName { get; set; } = string.Empty;
    public string EvaluatedBy { get; set; } = string.Empty;
    public DateTime EvaluatedAt { get; set; }
    public string? Notes { get; set; }
    public string? AgentName { get; set; }
    public string? CallReference { get; set; }
    public DateTime? CallDate { get; set; }
    public int? CallDurationSeconds { get; set; }
    public string? OverallReasoning { get; set; }
    public string? SentimentJson { get; set; }
    public string? FieldReasoningJson { get; set; }
    public List<EvaluationScoreDto> Scores { get; set; } = new();
    public double TotalScore { get; set; }
    public double MaxPossibleScore { get; set; }
    public List<EvaluationResultSectionDto> Sections { get; set; } = new();
}

public class EvaluationResultSectionDto
{
    public string Title { get; set; } = string.Empty;
    public List<EvaluationResultFieldScoreDto> Fields { get; set; } = new();
}

public class EvaluationResultFieldScoreDto
{
    public int FieldId { get; set; }
    public string FieldLabel { get; set; } = string.Empty;
    public int MaxRating { get; set; }
    public string Value { get; set; } = string.Empty;
    public double? NumericValue { get; set; }
}

public class CreateEvaluationResultDto
{
    public int FormId { get; set; }
    public string EvaluatedBy { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? AgentName { get; set; }
    public string? CallReference { get; set; }
    public DateTime? CallDate { get; set; }
    public string? OverallReasoning { get; set; }
    public string? SentimentJson { get; set; }
    public string? FieldReasoningJson { get; set; }
    public List<CreateEvaluationScoreDto> Scores { get; set; } = new();
}
