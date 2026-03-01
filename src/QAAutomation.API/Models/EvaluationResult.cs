using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QAAutomation.API.Models;

public class EvaluationResult
{
    public int Id { get; set; }

    public int FormId { get; set; }

    public EvaluationForm Form { get; set; } = null!;

    [Required]
    public string EvaluatedBy { get; set; } = string.Empty;

    public DateTime EvaluatedAt { get; set; }

    public string? Notes { get; set; }

    public string? AgentName { get; set; }      // The agent/employee being evaluated
    public string? CallReference { get; set; }  // Call ID / interaction reference
    public DateTime? CallDate { get; set; }     // Date of the evaluated call

    public ICollection<EvaluationScore> Scores { get; set; } = new List<EvaluationScore>();

    [NotMapped]
    public double TotalScore => Scores.Sum(s => s.NumericValue ?? 0);

    [NotMapped]
    public double MaxPossibleScore => Scores
        .Where(s => s.Field != null && s.Field.FieldType == FieldType.Rating)
        .Sum(s => s.Field.MaxRating);
}
