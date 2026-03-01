namespace QAAutomation.API.DTOs;

public class EvaluationResultDto
{
    public int Id { get; set; }
    public int FormId { get; set; }
    public string EvaluatedBy { get; set; } = string.Empty;
    public DateTime EvaluatedAt { get; set; }
    public string? Notes { get; set; }
    public List<EvaluationScoreDto> Scores { get; set; } = new();
    public double TotalScore { get; set; }
    public double MaxPossibleScore { get; set; }
}

public class CreateEvaluationResultDto
{
    public int FormId { get; set; }
    public string EvaluatedBy { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<CreateEvaluationScoreDto> Scores { get; set; } = new();
}
