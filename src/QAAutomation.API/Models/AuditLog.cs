using System.ComponentModel.DataAnnotations;

namespace QAAutomation.API.Models;

/// <summary>
/// Immutable audit log entry. Every PII/SPII protection event and every external
/// API call made by the platform is captured here, scoped to the tenant project.
/// </summary>
public class AuditLog
{
    public int Id { get; set; }

    // ── Tenant scoping ────────────────────────────────────────────────────────

    /// <summary>The project (tenant) this event belongs to. Null for system-level events.</summary>
    public int? ProjectId { get; set; }

    // ── What happened ─────────────────────────────────────────────────────────

    /// <summary>
    /// High-level category:
    /// "PiiEvent"       — PII/SPII protection triggered (detect / redact / block).
    /// "ExternalApiCall"— outbound call to LLM, speech-to-text, SFTP, SharePoint, or recording platform.
    /// </summary>
    [Required, MaxLength(30)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Detailed event type, e.g.:
    ///   PiiEvent:        "PiiDetected" | "PiiRedacted" | "PiiBlocked"
    ///   ExternalApiCall: "LlmAudit" | "LlmSentiment" | "SpeechTranscription"
    ///                  | "UrlFetch" | "SftpFetch" | "SharePointFetch"
    ///                  | "VerintFetch" | "NiceFetch" | "OzonetelFetch"
    /// </summary>
    [Required, MaxLength(50)]
    public string EventType { get; set; } = string.Empty;

    /// <summary>"Success" | "Failure" | "Blocked" | "Redacted" | "Detected"</summary>
    [Required, MaxLength(20)]
    public string Outcome { get; set; } = string.Empty;

    // ── Actor ─────────────────────────────────────────────────────────────────

    /// <summary>Username or "pipeline" / "system" that triggered the event.</summary>
    [MaxLength(200)]
    public string? Actor { get; set; }

    // ── PII-specific fields ───────────────────────────────────────────────────

    /// <summary>
    /// Comma-separated PII types detected (e.g. "EMAIL,PHONE,SSN").
    /// Populated for PiiEvent category entries only.
    /// </summary>
    [MaxLength(500)]
    public string? PiiTypesDetected { get; set; }

    // ── External API-specific fields ──────────────────────────────────────────

    /// <summary>HTTP method used (GET, POST, …). Null for non-HTTP external calls (SFTP, etc.).</summary>
    [MaxLength(10)]
    public string? HttpMethod { get; set; }

    /// <summary>Endpoint URL or host (API key / credentials are never stored).</summary>
    [MaxLength(1000)]
    public string? Endpoint { get; set; }

    /// <summary>HTTP status code returned by the external service. Null for non-HTTP calls.</summary>
    public int? HttpStatusCode { get; set; }

    /// <summary>Round-trip duration in milliseconds.</summary>
    public long? DurationMs { get; set; }

    /// <summary>Name of the AI provider / connector (e.g. "AzureOpenAI", "GoogleGemini", "Verint").</summary>
    [MaxLength(100)]
    public string? Provider { get; set; }

    // ── General detail ────────────────────────────────────────────────────────

    /// <summary>
    /// Optional extra context as free text (e.g. form name, call reference, error message).
    /// PII is never written here — only metadata.
    /// </summary>
    [MaxLength(2000)]
    public string? Details { get; set; }

    // ── Timestamp ─────────────────────────────────────────────────────────────

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
