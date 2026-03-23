using Microsoft.EntityFrameworkCore;
using QAAutomation.API.Data;
using QAAutomation.API.Models;

namespace QAAutomation.API.Services;

/// <summary>
/// Persists audit log entries to the AuditLog table.
/// Uses a dedicated DbContext scope so logging never interferes with
/// the caller's transaction or unit-of-work.
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(IDbContextFactory<AppDbContext> dbFactory, ILogger<AuditLogService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task LogPiiEventAsync(
        int? projectId,
        string eventType,
        string outcome,
        IEnumerable<string> piiTypes,
        string? actor = null,
        string? details = null,
        CancellationToken ct = default)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            db.AuditLogs.Add(new AuditLog
            {
                ProjectId        = projectId,
                Category         = "PiiEvent",
                EventType        = eventType,
                Outcome          = outcome,
                Actor            = actor,
                PiiTypesDetected = string.Join(",", piiTypes),
                Details          = details,
                OccurredAt       = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Logging must never crash the caller
            _logger.LogWarning(ex, "AuditLogService.LogPiiEventAsync failed — event not persisted");
        }
    }

    public async Task LogExternalApiCallAsync(
        int? projectId,
        string eventType,
        string outcome,
        string provider,
        string? endpoint,
        string? httpMethod = null,
        int? httpStatusCode = null,
        long? durationMs = null,
        string? actor = null,
        string? details = null,
        CancellationToken ct = default)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            db.AuditLogs.Add(new AuditLog
            {
                ProjectId      = projectId,
                Category       = "ExternalApiCall",
                EventType      = eventType,
                Outcome        = outcome,
                Provider       = provider,
                Endpoint       = SanitiseEndpoint(endpoint),
                HttpMethod     = httpMethod,
                HttpStatusCode = httpStatusCode,
                DurationMs     = durationMs,
                Actor          = actor,
                Details        = details,
                OccurredAt     = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AuditLogService.LogExternalApiCallAsync failed — event not persisted");
        }
    }

    // Remove query-string parameters that might contain credentials before storing the URL
    private static string? SanitiseEndpoint(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return url;
        try
        {
            var uri = new Uri(url, UriKind.RelativeOrAbsolute);
            if (uri.IsAbsoluteUri)
                return new UriBuilder(uri) { Query = string.Empty, Password = string.Empty }.Uri.ToString();
        }
        catch { /* fall through — store as-is but truncated */ }
        // For non-URL strings (e.g. SFTP host:path) just return as-is
        return url.Length > 500 ? url[..500] : url;
    }
}
