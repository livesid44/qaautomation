using System.ComponentModel.DataAnnotations;

namespace QAAutomation.API.DTOs;

// ── LLM generation request ─────────────────────────────────────────────────────

/// <summary>Triggers LLM generation of training content and MCQ for a training plan.</summary>
public class TniGenerateRequestDto
{
    /// <summary>Optional: override the number of MCQ questions to generate (default 5).</summary>
    public int QuestionCount { get; set; } = 5;
}

// ── Assessment question (returned to the UI) ──────────────────────────────────

public class TniAssessmentQuestionDto
{
    public int Index { get; set; }
    public string Question { get; set; } = string.Empty;
    /// <summary>List of option texts (A, B, C, D …).</summary>
    public List<string> Options { get; set; } = new();
    /// <summary>Excluded when serving to an agent; included for reviewer/admin views.</summary>
    public int? CorrectIndex { get; set; }
    public string? Explanation { get; set; }
}

// ── Assessment payload returned to UI ────────────────────────────────────────

public class TniAssessmentDto
{
    public int TrainingPlanId { get; set; }
    public string TrainingPlanTitle { get; set; } = string.Empty;
    /// <summary>The LLM-generated training material the agent should read before attempting.</summary>
    public string TrainingContent { get; set; } = string.Empty;
    public List<TniAssessmentQuestionDto> Questions { get; set; } = new();
    public int PassMark { get; set; }
    public DateTime? GeneratedAt { get; set; }
    /// <summary>Most recent attempt by the requesting agent (null if not yet attempted).</summary>
    public TniAttemptResultDto? LatestAttempt { get; set; }
}

// ── Attempt submission ────────────────────────────────────────────────────────

public class TniSubmitAttemptDto
{
    [Required, MaxLength(200)]
    public string AgentUsername { get; set; } = string.Empty;

    /// <summary>Selected option index per question, parallel to Questions array.</summary>
    [Required]
    public List<int> Answers { get; set; } = new();
}

// ── Attempt result ────────────────────────────────────────────────────────────

public class TniAttemptResultDto
{
    public int AttemptId { get; set; }
    public int TrainingPlanId { get; set; }
    public string AgentUsername { get; set; } = string.Empty;
    public int CorrectAnswers { get; set; }
    public int TotalQuestions { get; set; }
    public double ScorePercent { get; set; }
    public string Result { get; set; } = string.Empty;   // "Pass" or "Fail"
    public DateTime AttemptedAt { get; set; }
    /// <summary>Per-question feedback (correct answer + explanation) shown after submission.</summary>
    public List<TniQuestionFeedbackDto> Feedback { get; set; } = new();
}

public class TniQuestionFeedbackDto
{
    public int Index { get; set; }
    public string Question { get; set; } = string.Empty;
    public int AgentAnswer { get; set; }
    public int CorrectAnswer { get; set; }
    public bool IsCorrect { get; set; }
    public string? Explanation { get; set; }
}

// ── TNI Dashboard ────────────────────────────────────────────────────────────

public class TniDashboardDto
{
    public int TotalPlans { get; set; }
    public int PlansWithAssessment { get; set; }
    public int PendingAssessments { get; set; }
    public int PassedAssessments { get; set; }
    public int FailedAssessments { get; set; }
    public List<TniDashboardItemDto> Items { get; set; } = new();
}

public class TniDashboardItemDto
{
    public int PlanId { get; set; }
    public string PlanTitle { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string? AgentUsername { get; set; }
    public string PlanStatus { get; set; } = string.Empty;
    public bool HasAssessment { get; set; }
    /// <summary>"Pending" | "Pass" | "Fail" — based on the most recent attempt.</summary>
    public string AssessmentStatus { get; set; } = "Pending";
    public double? LatestScore { get; set; }
    public DateTime? LatestAttemptAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? ProjectId { get; set; }
}
