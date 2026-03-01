namespace QAAutomation.API.DTOs;

public class EvaluationScoreDto
{
    public int Id { get; set; }
    public int ResultId { get; set; }
    public int FieldId { get; set; }
    public string Value { get; set; } = string.Empty;
    public double? NumericValue { get; set; }
}

public class CreateEvaluationScoreDto
{
    public int FieldId { get; set; }
    public string Value { get; set; } = string.Empty;
    public double? NumericValue { get; set; }
}
