using System.ComponentModel.DataAnnotations;

namespace QAAutomation.API.Models;

/// <summary>
/// Per-parameter human score captured during a human review session.
/// Records both the original AI score and the human reviewer's override score
/// so that both can be retained and compared in the analytics dashboard.
/// </summary>
public class HumanFieldScore
{
    public int Id { get; set; }

    /// <summary>The human review item this score belongs to.</summary>
    public int HumanReviewItemId { get; set; }
    public HumanReviewItem? HumanReviewItem { get; set; }

    /// <summary>The form field (parameter) being scored.</summary>
    public int FieldId { get; set; }
    public FormField? Field { get; set; }

    /// <summary>AI-generated score at the time of review (copied from EvaluationScore for log integrity).</summary>
    public double AiScore { get; set; }

    /// <summary>Human reviewer's score for this parameter.</summary>
    public double HumanScore { get; set; }

    /// <summary>Optional per-parameter comment from the reviewer explaining the score adjustment.</summary>
    [MaxLength(1000)]
    public string? Comment { get; set; }
}
