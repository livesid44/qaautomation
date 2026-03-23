using System.ComponentModel.DataAnnotations;

namespace QAAutomation.API.DTOs;

// ── Read DTOs ─────────────────────────────────────────────────────────────────

public class CallPipelineJobDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public int FormId { get; set; }
    public string? FormName { get; set; }
    public int? ProjectId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int TotalItems { get; set; }
    public int CompletedItems { get; set; }
    public int FailedItems { get; set; }
    public List<CallPipelineItemDto> Items { get; set; } = new();
}

public class CallPipelineItemDto
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public string? SourceReference { get; set; }
    public string? AgentName { get; set; }
    public string? CallReference { get; set; }
    public DateTime? CallDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int? EvaluationResultId { get; set; }
    public double? ScorePercent { get; set; }
    public string? AiReasoning { get; set; }
}

// ── Create DTOs ───────────────────────────────────────────────────────────────

/// <summary>Request body for submitting a batch of recording or transcript URLs.</summary>
public class CreateBatchUrlJobDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Evaluation form to use for scoring each call.</summary>
    public int FormId { get; set; }

    public int? ProjectId { get; set; }

    /// <summary>Who is submitting this batch (username or system identity).</summary>
    [Required, MaxLength(200)]
    public string SubmittedBy { get; set; } = string.Empty;

    /// <summary>
    /// List of URL items to process. Each entry is a recording/transcript URL
    /// with optional metadata that overrides what the AI extracts automatically.
    /// </summary>
    [Required, MinLength(1)]
    public List<BatchUrlItemDto> Items { get; set; } = new();
}

public class BatchUrlItemDto
{
    /// <summary>
    /// URL of the call recording (e.g., .mp3/.wav served over HTTPS) or transcript
    /// (.txt/.json). The pipeline will attempt to fetch the content at this URL.
    /// Verint/NICE/Ozonetel direct-download links are accepted here.
    /// </summary>
    [Required, MaxLength(2000)]
    public string Url { get; set; } = string.Empty;

    public string? AgentName { get; set; }
    public string? CallReference { get; set; }
    public DateTime? CallDate { get; set; }
}

/// <summary>Request body to create a pipeline job that reads from a configured connector (SFTP / SharePoint).</summary>
public class CreateConnectorJobDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>"SFTP" | "SharePoint" | "Verint" | "NICE" | "Ozonetel"</summary>
    [Required, MaxLength(50)]
    public string SourceType { get; set; } = string.Empty;

    public int FormId { get; set; }
    public int? ProjectId { get; set; }

    [Required, MaxLength(200)]
    public string SubmittedBy { get; set; } = string.Empty;

    // ── SFTP ──────────────────────────────────────────────────────────────────
    public string? SftpHost { get; set; }
    public int? SftpPort { get; set; }
    public string? SftpUsername { get; set; }
    public string? SftpPassword { get; set; }
    /// <summary>Remote directory path to scan for transcript files.</summary>
    public string? SftpPath { get; set; }

    // ── SharePoint ────────────────────────────────────────────────────────────
    public string? SharePointSiteUrl { get; set; }
    public string? SharePointClientId { get; set; }
    public string? SharePointClientSecret { get; set; }
    public string? SharePointLibraryName { get; set; }

    // ── Recording platform (Verint / NICE / Ozonetel) ─────────────────────────
    public string? RecordingPlatformUrl { get; set; }
    public string? RecordingPlatformApiKey { get; set; }
    public string? RecordingPlatformTenantId { get; set; }

    // ── Optional date range filter ─────────────────────────────────────────────
    public string? FilterFromDate { get; set; }
    public string? FilterToDate { get; set; }
}

// ── SSE progress event ────────────────────────────────────────────────────────

/// <summary>
/// One Server-Sent Event payload emitted by GET /api/callpipeline/{id}/progress
/// for each item that finishes processing.
/// </summary>
public class PipelineProgressEventDto
{
    public int ItemId { get; set; }
    public int JobId { get; set; }
    public string ItemStatus { get; set; } = string.Empty;   // "Completed" | "Failed"
    public string? AgentName { get; set; }
    public string? CallReference { get; set; }
    public double? ScorePercent { get; set; }
    public string? ErrorMessage { get; set; }
    public int CompletedSoFar { get; set; }
    public int TotalItems { get; set; }
    public string JobStatus { get; set; } = string.Empty;    // "Running" | "Completed" | "Failed"
}
