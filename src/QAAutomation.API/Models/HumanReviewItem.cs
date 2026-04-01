using System.ComponentModel.DataAnnotations;

namespace QAAutomation.API.Models;

/// <summary>
/// A single call evaluation that has been selected (sampled) for human QA review.
/// Created automatically when a <see cref="SamplingPolicy"/> is applied, or manually
/// by an admin.
/// </summary>
public class HumanReviewItem
{
    public int Id { get; set; }

    /// <summary>The AI-generated evaluation result that needs human review.</summary>
    public int EvaluationResultId { get; set; }
    public EvaluationResult? EvaluationResult { get; set; }

    /// <summary>The sampling policy that selected this item (null if sampled manually).</summary>
    public int? SamplingPolicyId { get; set; }
    public SamplingPolicy? SamplingPolicy { get; set; }

    public DateTime SampledAt { get; set; } = DateTime.UtcNow;

    [MaxLength(200)]
    public string SampledBy { get; set; } = "system";

    /// <summary>
    /// Optional username of the QA analyst assigned to review this item.
    /// Null = unassigned (any QA user can pick it up).
    /// </summary>
    [MaxLength(200)]
    public string? AssignedTo { get; set; }

    /// <summary>
    /// "Pending"  — waiting for a reviewer to pick it up.
    /// "InReview" — a QA user has opened it but not submitted a verdict yet.
    /// "Reviewed" — reviewer has submitted their comment and verdict.
    /// </summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = "Pending";

    /// <summary>Free-text comment from the human reviewer.</summary>
    public string? ReviewerComment { get; set; }

    /// <summary>
    /// "Agree"    — reviewer agrees with the AI scoring.
    /// "Disagree" — reviewer disagrees (see ReviewerComment for reason).
    /// "Partial"  — partially agree.
    /// Null until reviewed.
    /// </summary>
    [MaxLength(20)]
    public string? ReviewVerdict { get; set; }

    [MaxLength(200)]
    public string? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    /// <summary>Per-parameter human scores submitted during the review. Empty until the review is submitted.</summary>
    public ICollection<HumanFieldScore> FieldScores { get; set; } = new List<HumanFieldScore>();
}
