using System.ComponentModel.DataAnnotations;

namespace QAAutomation.API.Models;

/// <summary>
/// A single actionable item (observation / recommendation) within a <see cref="TrainingPlan"/>.
/// Each item has its own lifecycle so the trainer can mark progress item-by-item.
/// </summary>
public class TrainingPlanItem
{
    public int Id { get; set; }

    public int TrainingPlanId { get; set; }
    public TrainingPlan? TrainingPlan { get; set; }

    /// <summary>The skill area / category this item targets (e.g. "Compliance", "Communication").</summary>
    [MaxLength(200)]
    public string TargetArea { get; set; } = string.Empty;

    /// <summary>
    /// "Observation" — describes a gap or issue observed in the audit.
    /// "Recommendation" — prescriptive action to address the gap.
    /// </summary>
    [Required, MaxLength(30)]
    public string ItemType { get; set; } = "Observation";

    /// <summary>Full text of the observation or recommendation.</summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// "Pending"    — not yet started.
    /// "InProgress" — trainer has begun addressing this item.
    /// "Done"       — trainer marked as complete.
    /// </summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = "Pending";

    public int Order { get; set; }

    [MaxLength(200)]
    public string? CompletedBy { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>Trainer notes captured when marking the item done.</summary>
    public string? CompletionNotes { get; set; }
}
