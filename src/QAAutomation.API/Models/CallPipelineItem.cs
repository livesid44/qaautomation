using System.ComponentModel.DataAnnotations;

namespace QAAutomation.API.Models;

/// <summary>
/// Represents a single recording or transcript within a <see cref="CallPipelineJob"/>.
/// One item corresponds to one QA evaluation result once processing completes.
/// </summary>
public class CallPipelineItem
{
    public int Id { get; set; }

    public int JobId { get; set; }
    public CallPipelineJob? Job { get; set; }

    /// <summary>
    /// For BatchUrl jobs: the original URL submitted by the caller.
    /// For connector-based jobs: a path/reference within the source system.
    /// </summary>
    [MaxLength(2000)]
    public string? SourceReference { get; set; }

    /// <summary>Agent name extracted from the transcript or provided by the caller.</summary>
    [MaxLength(200)]
    public string? AgentName { get; set; }

    /// <summary>Call reference / interaction ID (from URL metadata or extracted by AI).</summary>
    [MaxLength(200)]
    public string? CallReference { get; set; }

    /// <summary>Date of the call (from metadata or extracted by AI).</summary>
    public DateTime? CallDate { get; set; }

    /// <summary>
    /// Item status: Pending | Processing | Completed | Failed
    /// </summary>
    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }

    /// <summary>Error message if this individual item failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The EvaluationResult record created once this item is successfully processed.
    /// Null until processing completes.
    /// </summary>
    public int? EvaluationResultId { get; set; }
    public EvaluationResult? EvaluationResult { get; set; }

    /// <summary>Total QA score percentage for quick summary display.</summary>
    public double? ScorePercent { get; set; }

    /// <summary>AI-generated overall reasoning summary stored for quick display.</summary>
    public string? AiReasoning { get; set; }
}
