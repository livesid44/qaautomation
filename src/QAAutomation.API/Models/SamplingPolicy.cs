using System.ComponentModel.DataAnnotations;

namespace QAAutomation.API.Models;

/// <summary>
/// Defines how completed QA evaluations are sampled for human review.
/// Admins create policies that pick a percentage or count of evaluated calls
/// (optionally filtered by call-type / LOB) to send to the human review queue.
/// </summary>
public class SamplingPolicy
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the policy's purpose.
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Scopes this policy to one project. Null = applies across all projects.
    /// </summary>
    public int? ProjectId { get; set; }

    /// <summary>
    /// Filter by evaluation form name (contains match, case-insensitive).
    /// Null = no form filter (all call types are eligible).
    /// </summary>
    [MaxLength(200)]
    public string? CallTypeFilter { get; set; }

    /// <summary>
    /// Optional minimum call duration filter in seconds.
    /// Requires <see cref="EvaluationResult.CallDurationSeconds"/> to be populated.
    /// </summary>
    public int? MinDurationSeconds { get; set; }

    /// <summary>
    /// Optional maximum call duration filter in seconds.
    /// </summary>
    public int? MaxDurationSeconds { get; set; }

    /// <summary>
    /// "Percentage" — sample SampleValue % of eligible calls.
    /// "Count"      — sample up to SampleValue calls.
    /// </summary>
    [Required, MaxLength(20)]
    public string SamplingMethod { get; set; } = "Percentage";

    /// <summary>
    /// For Percentage: 0–100. For Count: a positive integer (stored as float for DB compatibility).
    /// </summary>
    public float SampleValue { get; set; } = 10f;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string CreatedBy { get; set; } = string.Empty;
}
