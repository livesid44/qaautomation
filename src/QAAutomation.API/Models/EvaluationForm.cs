using System.ComponentModel.DataAnnotations;

namespace QAAutomation.API.Models;

/// <summary>
/// Determines how the overall and section scores are calculated for an evaluation form.
/// </summary>
public enum ScoringMethod
{
    /// <summary>
    /// Standard proportional scoring: total score = sum of all field scores / max possible × 100.
    /// Used for Capital One and other standard forms.
    /// </summary>
    Generic = 0,

    /// <summary>
    /// Section-level auto-fail: if any field within a section scores 0, the entire section
    /// contributes 0 to the total. Each section's score is either its full sum or 0.
    /// Used for YouTube IQA forms where a single failure zeroes the category.
    /// </summary>
    SectionAutoFail = 1,
}

public class EvaluationForm
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>LOB this form belongs to. Null only for legacy data during migration.</summary>
    public int? LobId { get; set; }
    public Lob? Lob { get; set; }

    public ICollection<FormSection> Sections { get; set; } = new List<FormSection>();

    /// <summary>
    /// Controls how the total score and section scores are computed.
    /// Defaults to <see cref="ScoringMethod.Generic"/> (proportional sum).
    /// </summary>
    public ScoringMethod ScoringMethod { get; set; } = ScoringMethod.Generic;
}
