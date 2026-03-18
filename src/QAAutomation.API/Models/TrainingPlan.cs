using System.ComponentModel.DataAnnotations;

namespace QAAutomation.API.Models;

/// <summary>
/// A Training Need Identification (TNI) plan created by a quality manager
/// following an audit observation or recommendation.
/// A plan targets one call-center agent, is assigned to a trainer,
/// and is tracked through to closure.
/// </summary>
public class TrainingPlan
{
    public int Id { get; set; }

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Overall description, context, or root-cause analysis.</summary>
    public string? Description { get; set; }

    /// <summary>Display name of the call-center agent receiving this plan.</summary>
    [MaxLength(200)]
    public string AgentName { get; set; } = string.Empty;

    /// <summary>System username of the agent (for portal login filtering).</summary>
    [MaxLength(200)]
    public string? AgentUsername { get; set; }

    /// <summary>Display name of the trainer assigned to deliver the training.</summary>
    [MaxLength(200)]
    public string TrainerName { get; set; } = string.Empty;

    /// <summary>System username of the trainer.</summary>
    [MaxLength(200)]
    public string? TrainerUsername { get; set; }

    /// <summary>
    /// "Draft"      — being built by the quality manager.
    /// "Active"     — published and visible to agent + trainer.
    /// "InProgress" — trainer has acknowledged and started delivery.
    /// "Completed"  — trainer has marked all items done; awaiting QM closure.
    /// "Closed"     — QM has confirmed closure; full loop complete.
    /// </summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = "Draft";

    public DateTime? DueDate { get; set; }

    /// <summary>Scopes this plan to a project (optional).</summary>
    public int? ProjectId { get; set; }

    /// <summary>
    /// The audit evaluation that triggered this plan (optional — links context).
    /// </summary>
    public int? EvaluationResultId { get; set; }
    public EvaluationResult? EvaluationResult { get; set; }

    /// <summary>
    /// The human review item that triggered this plan (optional).
    /// </summary>
    public int? HumanReviewItemId { get; set; }
    public HumanReviewItem? HumanReviewItem { get; set; }

    [MaxLength(200)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(200)]
    public string? ClosedBy { get; set; }

    public DateTime? ClosedAt { get; set; }

    /// <summary>QM's closing remarks / confirmation note.</summary>
    public string? ClosingNotes { get; set; }

    public ICollection<TrainingPlanItem> Items { get; set; } = new List<TrainingPlanItem>();
}
