using System.ComponentModel.DataAnnotations;

namespace QAAutomation.API.DTOs;

// ── Sampling Policy ───────────────────────────────────────────────────────────

public class SamplingPolicyDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ProjectId { get; set; }
    public string? CallTypeFilter { get; set; }
    public int? MinDurationSeconds { get; set; }
    public int? MaxDurationSeconds { get; set; }
    public string SamplingMethod { get; set; } = "Percentage";
    public float SampleValue { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class CreateSamplingPolicyDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public int? ProjectId { get; set; }

    [MaxLength(200)]
    public string? CallTypeFilter { get; set; }

    public int? MinDurationSeconds { get; set; }
    public int? MaxDurationSeconds { get; set; }

    /// <summary>"Percentage" or "Count"</summary>
    [Required, MaxLength(20)]
    public string SamplingMethod { get; set; } = "Percentage";

    public float SampleValue { get; set; } = 10f;

    public bool IsActive { get; set; } = true;

    [MaxLength(200)]
    public string CreatedBy { get; set; } = string.Empty;
}

public class UpdateSamplingPolicyDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public int? ProjectId { get; set; }

    [MaxLength(200)]
    public string? CallTypeFilter { get; set; }

    public int? MinDurationSeconds { get; set; }
    public int? MaxDurationSeconds { get; set; }

    [Required, MaxLength(20)]
    public string SamplingMethod { get; set; } = "Percentage";

    public float SampleValue { get; set; } = 10f;

    public bool IsActive { get; set; } = true;
}

// ── Human Review ──────────────────────────────────────────────────────────────

public class HumanReviewItemDto
{
    public int Id { get; set; }
    public int EvaluationResultId { get; set; }
    public int? SamplingPolicyId { get; set; }
    public string? SamplingPolicyName { get; set; }
    public DateTime SampledAt { get; set; }
    public string SampledBy { get; set; } = string.Empty;
    public string? AssignedTo { get; set; }
    public string Status { get; set; } = "Pending";
    public string? ReviewerComment { get; set; }
    public string? ReviewVerdict { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }

    // Embedded AI audit summary
    public string? AgentName { get; set; }
    public string? CallReference { get; set; }
    public DateTime? CallDate { get; set; }
    public string? FormName { get; set; }
    public double? AiScorePercent { get; set; }
    public string? AiReasoning { get; set; }
    public int? ProjectId { get; set; }
}

/// <summary>Request body for submitting a human review verdict.</summary>
public class SubmitReviewDto
{
    public string? ReviewerComment { get; set; }

    /// <summary>"Agree" | "Disagree" | "Partial"</summary>
    [Required, MaxLength(20)]
    public string ReviewVerdict { get; set; } = "Agree";

    [Required, MaxLength(200)]
    public string ReviewedBy { get; set; } = string.Empty;
}

/// <summary>
/// Result returned after applying a sampling policy — summarises how many items were sampled.
/// </summary>
public class SamplingApplyResultDto
{
    public int PolicyId { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public int EligibleCount { get; set; }
    public int SampledCount { get; set; }
    public int AlreadySampledCount { get; set; }
}
