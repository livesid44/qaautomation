namespace QAAutomation.API.Models;

public class EvaluationScore
{
    public int Id { get; set; }

    public int ResultId { get; set; }

    public EvaluationResult Result { get; set; } = null!;

    public int FieldId { get; set; }

    public FormField Field { get; set; } = null!;

    public string Value { get; set; } = string.Empty;

    public double? NumericValue { get; set; }
}
