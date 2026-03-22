namespace QAAutomation.API.Services;

/// <summary>Records PII/SPII protection events and external API calls to the AuditLog table.</summary>
public interface IAuditLogService
{
    /// <summary>Log a PII/SPII protection event (detected, redacted, or blocked).</summary>
    Task LogPiiEventAsync(
        int? projectId,
        string eventType,       // "PiiDetected" | "PiiRedacted" | "PiiBlocked"
        string outcome,         // "Detected" | "Redacted" | "Blocked"
        IEnumerable<string> piiTypes,
        string? actor = null,
        string? details = null,
        CancellationToken ct = default);

    /// <summary>Log an outbound call to an external API / connector.</summary>
    Task LogExternalApiCallAsync(
        int? projectId,
        string eventType,       // "LlmAudit" | "LlmSentiment" | "SpeechTranscription" | "UrlFetch" | …
        string outcome,         // "Success" | "Failure"
        string provider,        // "AzureOpenAI" | "GoogleGemini" | "AzureSpeech" | "GoogleSpeech" | …
        string? endpoint,       // URL / host — never contains secrets/keys
        string? httpMethod = null,
        int? httpStatusCode = null,
        long? durationMs = null,
        string? actor = null,
        string? details = null,
        CancellationToken ct = default);
}
