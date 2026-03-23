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
    /// <summary>Call recording duration in seconds (populated by the pipeline when available).</summary>
    public int? CallDurationSeconds { get; set; }

    /// <summary>Overall AI quality assessment summary saved at audit time.</summary>
    public string? OverallReasoning { get; set; }

    /// <summary>JSON-serialized SentimentViewModel stored when an AI audit is saved.</summary>
    public string? SentimentJson { get; set; }

    /// <summary>JSON array [{fieldId, reasoning}] — per-field AI reasoning stored at save time.</summary>
    public string? FieldReasoningJson { get; set; }

    public ICollection<EvaluationScore> Scores { get; set; } = new List<EvaluationScore>();

    [NotMapped]
    public double TotalScore => Scores.Sum(s => s.NumericValue ?? 0);

    [NotMapped]
    public double MaxPossibleScore => Scores
        .Where(s => s.Field != null && s.Field.FieldType == FieldType.Rating)
        .Sum(s => s.Field.MaxRating);
}
