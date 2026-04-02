using System.ComponentModel.DataAnnotations;

namespace QAAutomation.API.Models;

/// <summary>
/// Records one agent attempt at the LLM-generated MCQ assessment attached to a training plan.
/// Multiple attempts are allowed; the latest result is surfaced in the TNI dashboard.
/// </summary>
public class TniAssessmentAttempt
{
    public int Id { get; set; }

    public int TrainingPlanId { get; set; }
    public TrainingPlan? TrainingPlan { get; set; }

    /// <summary>Username of the agent who took the assessment.</summary>
    [MaxLength(200)]
    public string AgentUsername { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of the agent's selected option indices, parallel to AssessmentJson questions.
    /// e.g. [1, 0, 2, 3]
    /// </summary>
    public string AnswersJson { get; set; } = "[]";

    /// <summary>Number of correct answers.</summary>
    public int CorrectAnswers { get; set; }

    /// <summary>Total questions in the assessment at the time of the attempt.</summary>
    public int TotalQuestions { get; set; }

    /// <summary>Percentage score (0–100).</summary>
    public double ScorePercent { get; set; }

    /// <summary>"Pass" or "Fail"</summary>
    [MaxLength(10)]
    public string Result { get; set; } = string.Empty;

    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
}
