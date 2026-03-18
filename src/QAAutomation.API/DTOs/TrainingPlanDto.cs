using System.ComponentModel.DataAnnotations;

namespace QAAutomation.API.DTOs;

// ── Read DTOs ─────────────────────────────────────────────────────────────────

public class TrainingPlanDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string? AgentUsername { get; set; }
    public string TrainerName { get; set; } = string.Empty;
    public string? TrainerUsername { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime? DueDate { get; set; }
    public int? ProjectId { get; set; }
    public int? EvaluationResultId { get; set; }
    public int? HumanReviewItemId { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? ClosedBy { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ClosingNotes { get; set; }
    public List<TrainingPlanItemDto> Items { get; set; } = new();
    public int TotalItems { get; set; }
    public int CompletedItems { get; set; }
}

public class TrainingPlanItemDto
{
    public int Id { get; set; }
    public int TrainingPlanId { get; set; }
    public string TargetArea { get; set; } = string.Empty;
    public string ItemType { get; set; } = "Observation";
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int Order { get; set; }
    public string? CompletedBy { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletionNotes { get; set; }
}

// ── Create / Update DTOs ──────────────────────────────────────────────────────

public class CreateTrainingPlanDto
{
    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required, MaxLength(200)]
    public string AgentName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? AgentUsername { get; set; }

    [Required, MaxLength(200)]
    public string TrainerName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? TrainerUsername { get; set; }

    public DateTime? DueDate { get; set; }

    public int? ProjectId { get; set; }

    /// <summary>Optional link to the audit that triggered this plan.</summary>
    public int? EvaluationResultId { get; set; }

    /// <summary>Optional link to the human review item that triggered this plan.</summary>
    public int? HumanReviewItemId { get; set; }

    [Required, MaxLength(200)]
    public string CreatedBy { get; set; } = string.Empty;

    [Required, MinLength(1)]
    public List<CreateTrainingPlanItemDto> Items { get; set; } = new();
}

public class CreateTrainingPlanItemDto
{
    [MaxLength(200)]
    public string TargetArea { get; set; } = string.Empty;

    /// <summary>"Observation" or "Recommendation"</summary>
    [Required, MaxLength(30)]
    public string ItemType { get; set; } = "Observation";

    [Required]
    public string Content { get; set; } = string.Empty;

    public int Order { get; set; }
}

public class UpdateTrainingPlanDto
{
    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required, MaxLength(200)]
    public string AgentName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? AgentUsername { get; set; }

    [Required, MaxLength(200)]
    public string TrainerName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? TrainerUsername { get; set; }

    public DateTime? DueDate { get; set; }

    public int? ProjectId { get; set; }
}

public class UpdateTrainingPlanItemDto
{
    [MaxLength(200)]
    public string TargetArea { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string ItemType { get; set; } = "Observation";

    [Required]
    public string Content { get; set; } = string.Empty;

    public int Order { get; set; }
}

/// <summary>Request body for closing a training plan (QM confirms completion).</summary>
public class CloseTrainingPlanDto
{
    [Required, MaxLength(200)]
    public string ClosedBy { get; set; } = string.Empty;

    public string? ClosingNotes { get; set; }
}

/// <summary>Request body for marking a plan item done (trainer).</summary>
public class CompleteTrainingPlanItemDto
{
    [Required, MaxLength(200)]
    public string CompletedBy { get; set; } = string.Empty;

    public string? CompletionNotes { get; set; }
}

/// <summary>Request to change a plan's status (e.g. Draft→Active, Active→InProgress).</summary>
public class UpdateTrainingPlanStatusDto
{
    /// <summary>"Draft" | "Active" | "InProgress" | "Completed" | "Closed"</summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string UpdatedBy { get; set; } = string.Empty;
}
