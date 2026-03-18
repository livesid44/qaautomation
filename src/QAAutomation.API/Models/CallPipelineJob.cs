using System.ComponentModel.DataAnnotations;

namespace QAAutomation.API.Models;

/// <summary>
/// Represents a batch pipeline job that fetches call recordings or transcripts
/// from a configured source and runs fully automated QA analysis without
/// any human in the loop.
/// </summary>
public class CallPipelineJob
{
    public int Id { get; set; }

    /// <summary>Human-readable label for this job.</summary>
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Source type: "BatchUrl" | "SFTP" | "SharePoint" | "Verint" | "NICE" | "Ozonetel"
    /// </summary>
    [Required, MaxLength(50)]
    public string SourceType { get; set; } = "BatchUrl";

    /// <summary>The evaluation form used to score each call.</summary>
    public int FormId { get; set; }
    public EvaluationForm? Form { get; set; }

    /// <summary>Project this job belongs to.</summary>
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }

    /// <summary>
    /// Overall job status: Pending | Running | Completed | Failed
    /// </summary>
    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>Who triggered this job (username).</summary>
    [MaxLength(200)]
    public string CreatedBy { get; set; } = string.Empty;

    // ── SFTP connector settings (copied from connector at job creation time) ──
    public string? SftpHost { get; set; }
    public int? SftpPort { get; set; }
    public string? SftpUsername { get; set; }
    public string? SftpPassword { get; set; }
    public string? SftpPath { get; set; }

    // ── SharePoint connector settings ─────────────────────────────────────────
    public string? SharePointSiteUrl { get; set; }
    public string? SharePointClientId { get; set; }
    public string? SharePointClientSecret { get; set; }
    public string? SharePointLibraryName { get; set; }

    // ── Verint / NICE / Ozonetel API settings ─────────────────────────────────
    /// <summary>Base URL of the recording platform API.</summary>
    public string? RecordingPlatformUrl { get; set; }
    /// <summary>API key or bearer token for the recording platform.</summary>
    public string? RecordingPlatformApiKey { get; set; }
    /// <summary>Tenant / organisation identifier required by some platforms.</summary>
    public string? RecordingPlatformTenantId { get; set; }

    /// <summary>Optional date range filter: from date (ISO 8601 string).</summary>
    [MaxLength(30)]
    public string? FilterFromDate { get; set; }

    /// <summary>Optional date range filter: to date (ISO 8601 string).</summary>
    [MaxLength(30)]
    public string? FilterToDate { get; set; }

    /// <summary>Error message if the job failed at the orchestration level.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Individual items (one per recording/transcript) in this job.</summary>
    public ICollection<CallPipelineItem> Items { get; set; } = new List<CallPipelineItem>();
}
