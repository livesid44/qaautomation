using Microsoft.EntityFrameworkCore;
using QAAutomation.API.Data;
using QAAutomation.API.DTOs;
using QAAutomation.API.Models;

namespace QAAutomation.API.Services;

/// <summary>
/// Orchestrates fully automated, human-out-of-the-loop QA pipeline jobs.
/// Supports:
///   • BatchUrl  — caller provides a list of transcript/recording URLs
///   • SFTP      — scans a remote SFTP directory for .txt transcript files
///   • SharePoint — lists files from a SharePoint document library
///   • Verint / NICE / Ozonetel — queries the recording platform REST API
///
/// For each source item the service:
///   1. Fetches / downloads the transcript text
///   2. Calls IAutoAuditService to score the transcript with the LLM
///   3. Persists an EvaluationResult record
///   4. Updates CallPipelineItem with the result reference and score
/// </summary>
public interface ICallPipelineService
{
    Task<CallPipelineJobDto> CreateBatchUrlJobAsync(CreateBatchUrlJobDto dto, CancellationToken ct = default);
    Task<CallPipelineJobDto> CreateConnectorJobAsync(CreateConnectorJobDto dto, CancellationToken ct = default);
    Task<CallPipelineJobDto> CreateFileUploadJobAsync(string name, int formId, int? projectId, string submittedBy,
        List<BatchUrlItemDto> items, CancellationToken ct = default);
    Task ProcessJobAsync(int jobId, CancellationToken ct = default);

    /// <summary>
    /// Resets a stale "Running" pipeline job (e.g. interrupted by an app restart)
    /// back to "Pending" and resets any items that are stuck in "Processing" to "Pending"
    /// so that a subsequent <see cref="ProcessJobAsync"/> call will re-process them.
    /// Returns false if the job is not found or is not in a resumable state.
    /// </summary>
    Task<bool> ResetStalledJobAsync(int jobId, CancellationToken ct = default);

    Task<CallPipelineJobDto?> GetJobAsync(int id);
    Task<List<CallPipelineJobDto>> ListJobsAsync(int? projectId = null);
}

/// <inheritdoc/>
public class CallPipelineService : ICallPipelineService
{
    private readonly AppDbContext _db;
    private readonly IAutoAuditService _auditService;
    private readonly IAzureSpeechService _speechService;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IAuditLogService _auditLog;
    private readonly PipelineProgressHub _progressHub;
    private readonly ILogger<CallPipelineService> _logger;

    // Maximum transcript length fed to the LLM (same cap as the interactive UI)
    private const int MaxTranscriptLength = 12_000;

    public CallPipelineService(
        AppDbContext db,
        IAutoAuditService auditService,
        IAzureSpeechService speechService,
        IHttpClientFactory httpFactory,
        IAuditLogService auditLog,
        PipelineProgressHub progressHub,
        ILogger<CallPipelineService> logger)
    {
        _db = db;
        _auditService = auditService;
        _speechService = speechService;
        _httpFactory = httpFactory;
        _auditLog = auditLog;
        _progressHub = progressHub;
        _logger = logger;
    }

    // ── Create jobs ───────────────────────────────────────────────────────────

    public async Task<CallPipelineJobDto> CreateBatchUrlJobAsync(CreateBatchUrlJobDto dto, CancellationToken ct = default)
    {
        var job = new CallPipelineJob
        {
            Name = dto.Name,
            SourceType = "BatchUrl",
            FormId = dto.FormId,
            ProjectId = dto.ProjectId,
            Status = "Pending",
            CreatedBy = dto.SubmittedBy,
            CreatedAt = DateTime.UtcNow,
            Items = dto.Items.Select(i => new CallPipelineItem
            {
                SourceReference = i.Url,
                AgentName = i.AgentName,
                CallReference = i.CallReference,
                CallDate = i.CallDate,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            }).ToList()
        };

        _db.CallPipelineJobs.Add(job);
        await _db.SaveChangesAsync(ct);
        return await MapJobToDto(job);
    }

    public async Task<CallPipelineJobDto> CreateFileUploadJobAsync(
        string name, int formId, int? projectId, string submittedBy,
        List<BatchUrlItemDto> items, CancellationToken ct = default)
    {
        var job = new CallPipelineJob
        {
            Name       = name,
            SourceType = "FileUpload",
            FormId     = formId,
            ProjectId  = projectId,
            Status     = "Pending",
            CreatedBy  = submittedBy,
            CreatedAt  = DateTime.UtcNow,
            Items      = items.Select(i => new CallPipelineItem
            {
                SourceReference = i.Url,
                AgentName       = i.AgentName,
                CallReference   = i.CallReference,
                CallDate        = i.CallDate,
                Status          = "Pending",
                CreatedAt       = DateTime.UtcNow
            }).ToList()
        };
        _db.CallPipelineJobs.Add(job);
        await _db.SaveChangesAsync(ct);
        return await MapJobToDto(job);
    }

    public async Task<CallPipelineJobDto> CreateConnectorJobAsync(CreateConnectorJobDto dto, CancellationToken ct = default)
    {
        var job = new CallPipelineJob
        {
            Name = dto.Name,
            SourceType = dto.SourceType,
            FormId = dto.FormId,
            ProjectId = dto.ProjectId,
            Status = "Pending",
            CreatedBy = dto.SubmittedBy,
            CreatedAt = DateTime.UtcNow,
            // Connector settings — stored encrypted-at-rest in SQLite
            SftpHost = dto.SftpHost,
            SftpPort = dto.SftpPort,
            SftpUsername = dto.SftpUsername,
            SftpPassword = dto.SftpPassword,
            SftpPath = dto.SftpPath,
            SharePointSiteUrl = dto.SharePointSiteUrl,
            SharePointClientId = dto.SharePointClientId,
            SharePointClientSecret = dto.SharePointClientSecret,
            SharePointLibraryName = dto.SharePointLibraryName,
            RecordingPlatformUrl = dto.RecordingPlatformUrl,
            RecordingPlatformApiKey = dto.RecordingPlatformApiKey,
            RecordingPlatformTenantId = dto.RecordingPlatformTenantId,
            FilterFromDate = dto.FilterFromDate,
            FilterToDate = dto.FilterToDate
        };

        _db.CallPipelineJobs.Add(job);
        await _db.SaveChangesAsync(ct);

        // For connector-based jobs, discover items immediately so the caller
        // can see what was found before triggering full processing.
        await DiscoverConnectorItemsAsync(job, ct);

        return await MapJobToDto(job);
    }

    // ── Process a job ─────────────────────────────────────────────────────────

    /// <summary>
    /// Runs all pending items in a job sequentially.
    /// The caller should fire this in a background task for large batches.
    /// </summary>
    public async Task ProcessJobAsync(int jobId, CancellationToken ct = default)
    {
        var job = await _db.CallPipelineJobs
            .Include(j => j.Items)
            .Include(j => j.Form)
                .ThenInclude(f => f!.Sections)
                    .ThenInclude(s => s.Fields)
            .Include(j => j.Form!.Lob)
            .FirstOrDefaultAsync(j => j.Id == jobId, ct);

        if (job is null)
        {
            _logger.LogWarning("ProcessJobAsync: job {JobId} not found", jobId);
            return;
        }

        if (job.Status == "Running")
        {
            _logger.LogWarning("ProcessJobAsync: job {JobId} is already running", jobId);
            return;
        }

        job.Status = "Running";
        job.StartedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Build form field definitions once for all items in the job
        var form = job.Form;
        if (form is null)
        {
            job.Status = "Failed";
            job.ErrorMessage = $"Evaluation form with id {job.FormId} not found.";
            job.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return;
        }

        // Load parameter metadata for LLM prompt enrichment
        var paramMap = await _db.Parameters
            .Where(p => p.IsActive)
            .Select(p => new { p.Name, p.EvaluationType, p.Description })
            .ToDictionaryAsync(p => p.Name, p => (p.EvaluationType, p.Description), ct);

        var fieldDefinitions = form.Sections
            .OrderBy(s => s.Order)
            .SelectMany(s => s.Fields.OrderBy(f => f.Order).Select(f =>
            {
                var (evalType, desc) = paramMap.TryGetValue(f.Label, out var pm) ? pm : ("LLM", (string?)null);
                return new AutoAuditFieldDefinition(
                    FieldId: f.Id,
                    Label: f.Label,
                    Description: desc,
                    MaxRating: f.MaxRating,
                    IsRequired: f.IsRequired,
                    SectionTitle: s.Title,
                    EvaluationType: evalType);
            }))
            .ToList();

        int errors = 0;
        int completed = 0;
        int total = job.Items.Count(i => i.Status == "Pending");
        foreach (var item in job.Items.Where(i => i.Status == "Pending"))
        {
            if (ct.IsCancellationRequested) break;
            await ProcessItemAsync(job, item, fieldDefinitions, form.Name, ct);
            if (item.Status == "Failed") errors++;
            completed++;
            await _db.SaveChangesAsync(ct);

            // Publish per-item progress for SSE subscribers
            _progressHub.Publish(job.Id, new PipelineProgressEventDto
            {
                ItemId        = item.Id,
                JobId         = job.Id,
                ItemStatus    = item.Status,
                AgentName     = item.AgentName,
                CallReference = item.CallReference,
                ScorePercent  = item.ScorePercent,
                ErrorMessage  = item.Status == "Failed" ? item.ErrorMessage : null,
                CompletedSoFar = completed,
                TotalItems    = total,
                JobStatus     = "Running"
            });
        }

        job.Status = errors == job.Items.Count ? "Failed" : "Completed";
        job.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Final event + close all subscriber channels
        _progressHub.Publish(job.Id, new PipelineProgressEventDto
        {
            ItemId        = 0,
            JobId         = job.Id,
            ItemStatus    = "Done",
            CompletedSoFar = completed,
            TotalItems    = total,
            JobStatus     = job.Status
        });
        _progressHub.Complete(job.Id);
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    public async Task<bool> ResetStalledJobAsync(int jobId, CancellationToken ct = default)
    {
        var job = await _db.CallPipelineJobs
            .Include(j => j.Items)
            .FirstOrDefaultAsync(j => j.Id == jobId, ct);

        if (job is null)
        {
            _logger.LogWarning("ResetStalledJobAsync: job {JobId} not found", jobId);
            return false;
        }

        // Only reset jobs that are genuinely stalled — i.e. "Running" but
        // not actively being processed (detected by having unfinished items).
        // Completed/Failed jobs must not be reset.
        if (job.Status != "Running")
        {
            _logger.LogWarning(
                "ResetStalledJobAsync: job {JobId} has status '{Status}' — only Running jobs can be reset",
                jobId, job.Status);
            return false;
        }

        job.Status = "Pending";
        // Items stuck mid-flight are re-queued; already completed/failed items are kept.
        foreach (var item in job.Items.Where(i => i.Status == "Processing"))
            item.Status = "Pending";

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "ResetStalledJobAsync: job {JobId} reset to Pending for re-processing", jobId);
        return true;
    }

    public async Task<CallPipelineJobDto?> GetJobAsync(int id)
    {
        var job = await _db.CallPipelineJobs
            .Include(j => j.Items)
            .Include(j => j.Form)
            .FirstOrDefaultAsync(j => j.Id == id);
        return job is null ? null : await MapJobToDto(job);
    }

    public async Task<List<CallPipelineJobDto>> ListJobsAsync(int? projectId = null)
    {
        var query = _db.CallPipelineJobs
            .Include(j => j.Items)
            .Include(j => j.Form)
            .AsQueryable();

        if (projectId.HasValue)
            query = query.Where(j => j.ProjectId == projectId.Value);

        var jobs = await query.OrderByDescending(j => j.CreatedAt).ToListAsync();
        return jobs.Select(j => MapJobToDtoSync(j)).ToList();
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Fetches or transcribes a single item and runs LLM scoring.
    /// If the source is audio, Azure Speech-to-Text is used to produce a transcript first.
    /// </summary>
    private async Task ProcessItemAsync(
        CallPipelineJob job,
        CallPipelineItem item,
        IList<AutoAuditFieldDefinition> fieldDefs,
        string formName,
        CancellationToken ct)
    {
        item.Status = "Processing";
        try
        {
            string? transcript;

            // Determine whether the item source is audio or text
            if (IsAudioItem(item.SourceReference))
            {
                _logger.LogInformation("Pipeline item {ItemId}: audio detected — transcribing via Azure Speech", item.Id);
                transcript = await TranscribeAudioItemAsync(job, item, ct);
                if (string.IsNullOrWhiteSpace(transcript))
                {
                    item.Status = "Failed";
                    item.ErrorMessage = "Audio transcription failed or Azure Speech is not configured. " +
                                        "Configure SpeechEndpoint and SpeechApiKey in AI Settings.";
                    item.ProcessedAt = DateTime.UtcNow;
                    return;
                }
            }
            else
            {
                transcript = await FetchTranscriptAsync(job, item, ct);
                if (string.IsNullOrWhiteSpace(transcript))
                {
                    item.Status = "Failed";
                    item.ErrorMessage = "Could not retrieve transcript content — response was empty.";
                    item.ProcessedAt = DateTime.UtcNow;
                    return;
                }
            }

            if (transcript.Length > MaxTranscriptLength)
                transcript = transcript[..MaxTranscriptLength] + "\n[TRANSCRIPT TRUNCATED]";

            var auditRequest = new AutoAuditRequestDto
            {
                FormId = job.FormId,
                Transcript = transcript,
                AgentName = item.AgentName,
                CallReference = item.CallReference,
                CallDate = item.CallDate,
                EvaluatedBy = $"pipeline:{job.Name}"
            };

            var result = await _auditService.AnalyzeTranscriptAsync(auditRequest, fieldDefs, formName, job.Form?.Lob?.ProjectId, ct);

            // Persist EvaluationResult
            var evalResult = new EvaluationResult
            {
                FormId = job.FormId,
                EvaluatedBy = $"pipeline:{job.Name}",
                EvaluatedAt = DateTime.UtcNow,
                AgentName = result.AgentName ?? item.AgentName,
                CallReference = item.CallReference,
                CallDate = item.CallDate,
                Notes = $"[Auto-Pipeline: {job.Name}] {result.OverallReasoning}",
                Scores = result.Fields.Select(f => new EvaluationScore
                {
                    FieldId = f.FieldId,
                    Value = f.FinalScore.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    NumericValue = f.FinalScore
                }).ToList()
            };
            _db.EvaluationResults.Add(evalResult);
            await _db.SaveChangesAsync(ct);

            item.EvaluationResultId = evalResult.Id;
            item.ScorePercent = result.ScorePercent;
            item.AiReasoning = result.OverallReasoning;
            item.Status = "Completed";
            item.ProcessedAt = DateTime.UtcNow;
            // Update AgentName from AI extraction if not provided up front
            if (string.IsNullOrEmpty(item.AgentName) && !string.IsNullOrEmpty(result.AgentName))
                item.AgentName = result.AgentName;
        }
        catch (OperationCanceledException)
        {
            item.Status = "Pending"; // re-queue on cancel
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline item {ItemId} (job {JobId}) failed", item.Id, job.Id);
            item.Status = "Failed";
            item.ErrorMessage = ex.Message;
            item.ProcessedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Returns true when the item's SourceReference URL appears to be an audio file
    /// (based on file extension in the URL path).
    /// </summary>
    private static bool IsAudioItem(string? sourceReference) =>
        !string.IsNullOrEmpty(sourceReference) && AudioFormatHelper.IsAudioUrl(sourceReference);

    /// <summary>
    /// Transcribes an audio item using Azure Speech-to-Text.
    /// For BatchUrl: passes the URL directly to the Azure Speech batch transcription API.
    /// For SFTP / SharePoint: downloads the file, then needs an accessible URL —
    ///   currently the pipeline passes the original SourceReference, which must be
    ///   an HTTPS-accessible URL for the batch transcription service to reach it.
    /// </summary>
    private async Task<string?> TranscribeAudioItemAsync(CallPipelineJob job, CallPipelineItem item, CancellationToken ct)
    {
        // For all source types, SourceReference should be the URL of the audio file.
        // The Azure Speech Batch Transcription API requires the audio to be accessible
        // over HTTPS, so SFTP paths that are not HTTP-accessible cannot be transcribed
        // directly — the caller should pre-stage audio files to an HTTPS-accessible location.
        var audioUrl = item.SourceReference ?? string.Empty;
        if (string.IsNullOrWhiteSpace(audioUrl)) return null;

        _logger.LogInformation("Transcribing audio: {Url} (job {JobId})", audioUrl, job.Id);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string? result = null;
        try
        {
            result = await _speechService.TranscribeAudioUrlAsync(audioUrl, ct);
            sw.Stop();
            await _auditLog.LogExternalApiCallAsync(
                job.Form?.Lob?.ProjectId, "SpeechTranscription",
                string.IsNullOrWhiteSpace(result) ? "Failure" : "Success",
                "SpeechService", audioUrl, "POST", null, sw.ElapsedMilliseconds,
                actor: $"pipeline:{job.Name}",
                details: $"Job: {job.Id}; Item: {item.Id}; Ref: {item.CallReference}",
                ct: ct);
        }
        catch
        {
            sw.Stop();
            await _auditLog.LogExternalApiCallAsync(
                job.Form?.Lob?.ProjectId, "SpeechTranscription", "Failure",
                "SpeechService", audioUrl, "POST", null, sw.ElapsedMilliseconds,
                actor: $"pipeline:{job.Name}",
                details: $"Job: {job.Id}; Item: {item.Id}",
                ct: ct);
            throw;
        }
        return result;
    }

    /// <summary>
    /// Fetches the transcript text for a pipeline item.
    /// Strategy:
    ///   • BatchUrl: HTTP GET the URL; treat the response body as plain text (transcript).
    ///   • SFTP: Download the file at SftpPath/SourceReference using Renci.SshNet (if configured).
    ///   • SharePoint / Verint / NICE / Ozonetel: Use the platform's REST API to get the transcript or
    ///     download a recording (recording files are treated as transcripts if the AI is configured to
    ///     process audio — otherwise a stub placeholder is returned so the item is not skipped).
    /// NOTE: Audio detection (IsAudioItem) is checked in ProcessItemAsync BEFORE this method is called,
    ///       so this method only handles text/JSON content.
    /// </summary>
    private async Task<string> FetchTranscriptAsync(CallPipelineJob job, CallPipelineItem item, CancellationToken ct)
    {
        var eventType = job.SourceType switch
        {
            "BatchUrl" or "FileUpload" => "UrlFetch",
            "SFTP"       => "SftpFetch",
            "SharePoint" => "SharePointFetch",
            "Verint"     => "VerintFetch",
            "NICE"       => "NiceFetch",
            "Ozonetel"   => "OzonetelFetch",
            _            => "ExternalFetch"
        };
        var endpoint = job.SourceType is "BatchUrl" or "FileUpload"
            ? (item.SourceReference?.StartsWith("data:", StringComparison.OrdinalIgnoreCase) == true ? "(inline transcript)" : item.SourceReference)
            : job.SharePointSiteUrl ?? job.SftpHost ?? job.SourceType;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var transcript = job.SourceType switch
            {
                "BatchUrl" or "FileUpload" => await FetchUrlAsync(item.SourceReference ?? string.Empty, ct),
                "SFTP"       => await FetchSftpAsync(job, item, ct),
                "SharePoint" => await FetchSharePointAsync(job, item, ct),
                "Verint" or "NICE" or "Ozonetel" => await FetchRecordingPlatformAsync(job, item, ct),
                _            => string.Empty
            };
            sw.Stop();
            await _auditLog.LogExternalApiCallAsync(
                job.Form?.Lob?.ProjectId, eventType,
                string.IsNullOrWhiteSpace(transcript) ? "Failure" : "Success",
                job.SourceType, endpoint, "GET", null, sw.ElapsedMilliseconds,
                actor: $"pipeline:{job.Name}",
                details: $"Job: {job.Id}; Item: {item.Id}; Ref: {item.SourceReference}",
                ct: ct);
            return transcript;
        }
        catch (Exception ex)
        {
            sw.Stop();
            await _auditLog.LogExternalApiCallAsync(
                job.Form?.Lob?.ProjectId, eventType, "Failure",
                job.SourceType, endpoint, "GET", null, sw.ElapsedMilliseconds,
                actor: $"pipeline:{job.Name}",
                details: $"Job: {job.Id}; Item: {item.Id}; Error: {ex.Message[..Math.Min(200, ex.Message.Length)]}",
                ct: ct);
            throw;
        }
    }

    /// <summary>
    /// HTTP-fetches a URL and returns the response body as text (plain-text transcript).
    /// If the server returns an audio Content-Type the caller (ProcessItemAsync) will
    /// have already routed to TranscribeAudioItemAsync, so this method only handles text.
    /// </summary>
    private async Task<string> FetchUrlAsync(string url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;

        // Inline transcript stored as a data URI: "data:text/plain,<url-encoded text>"
        // Used by the FileUpload pipeline so no HTTP fetch is needed.
        if (url.StartsWith("data:text/plain,", StringComparison.OrdinalIgnoreCase))
            return Uri.UnescapeDataString(url["data:text/plain,".Length..]);

        try
        {
            var client = _httpFactory.CreateClient("pipeline");
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            // If the server returns an audio content-type for a URL that didn't have an
            // audio extension we can't process it as text — signal to the caller.
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (AudioFormatHelper.IsAudioContentType(contentType))
            {
                _logger.LogWarning("FetchUrlAsync: URL {Url} returned audio content-type '{CT}' — " +
                                   "re-route to speech transcription is needed; item will be retried as audio.", url, contentType);
                // Return empty so the caller marks it failed with a helpful message
                return string.Empty;
            }

            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FetchUrlAsync failed for URL: {Url}", url);
            throw;
        }
    }

    /// <summary>
    /// Downloads a transcript file from SFTP.
    /// Requires Renci.SshNet — a lightweight .NET SFTP client.
    /// Falls back to a descriptive error if the library is not available or connection fails.
    /// </summary>
    private Task<string> FetchSftpAsync(CallPipelineJob job, CallPipelineItem item, CancellationToken ct)
    {
        // The SFTP connector is implemented via Renci.SshNet (optional dependency).
        // If the host is empty the job was created without SFTP config — return clear error.
        if (string.IsNullOrEmpty(job.SftpHost))
            return Task.FromResult(string.Empty);

        try
        {
            var port = job.SftpPort ?? 22;
            var remotePath = string.IsNullOrEmpty(item.SourceReference)
                ? job.SftpPath ?? "/"
                : item.SourceReference;

            using var client = new Renci.SshNet.SftpClient(
                job.SftpHost, port,
                job.SftpUsername ?? string.Empty,
                job.SftpPassword ?? string.Empty);

            client.Connect();
            try
            {
                using var stream = new System.IO.MemoryStream();
                client.DownloadFile(remotePath, stream);
                stream.Position = 0;
                return Task.FromResult(System.Text.Encoding.UTF8.GetString(stream.ToArray()));
            }
            finally
            {
                client.Disconnect();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SFTP fetch failed for {Path}", item.SourceReference);
            throw;
        }
    }

    /// <summary>
    /// Downloads a transcript file from a SharePoint document library via the SharePoint REST API.
    /// Uses OAuth2 client-credentials flow to obtain a token, then issues a GET request
    /// against the /GetFileByServerRelativeUrl endpoint.
    /// </summary>
    private async Task<string> FetchSharePointAsync(CallPipelineJob job, CallPipelineItem item, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(job.SharePointSiteUrl))
            return string.Empty;

        try
        {
            var client = _httpFactory.CreateClient("pipeline");

            // 1. Obtain OAuth2 bearer token via client credentials
            var tokenEndpoint = $"https://accounts.accesscontrol.windows.net/common/tokens/OAuth/2";
            var tokenResp = await client.PostAsync(tokenEndpoint, new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("grant_type",    "client_credentials"),
                new KeyValuePair<string,string>("client_id",     job.SharePointClientId ?? string.Empty),
                new KeyValuePair<string,string>("client_secret", job.SharePointClientSecret ?? string.Empty),
                new KeyValuePair<string,string>("resource",      job.SharePointSiteUrl)
            }), ct);

            string? accessToken = null;
            if (tokenResp.IsSuccessStatusCode)
            {
                var tokenDoc = System.Text.Json.JsonDocument.Parse(await tokenResp.Content.ReadAsStringAsync(ct));
                tokenDoc.RootElement.TryGetProperty("access_token", out var tok);
                accessToken = tok.GetString();
            }

            // 2. Build the SharePoint REST download URL
            var library = job.SharePointLibraryName ?? "Shared Documents";
            var filePath = item.SourceReference ?? string.Empty;
            var downloadUrl = $"{job.SharePointSiteUrl}/_api/web/GetFileByServerRelativeUrl('{library}/{filePath}')/$value";

            var req = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            if (!string.IsNullOrEmpty(accessToken))
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/plain"));

            using var fileResp = await client.SendAsync(req, ct);
            fileResp.EnsureSuccessStatusCode();
            return await fileResp.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SharePoint fetch failed for {File}", item.SourceReference);
            throw;
        }
    }

    /// <summary>
    /// Fetches transcript/recording text from Verint, NICE, or Ozonetel via their REST APIs.
    /// All three platforms expose a GET /transcripts/{id} or similar endpoint.
    /// The API key and base URL are stored in the job's RecordingPlatform fields.
    /// The SourceReference should be the call/interaction ID on the platform.
    /// </summary>
    private async Task<string> FetchRecordingPlatformAsync(CallPipelineJob job, CallPipelineItem item, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(job.RecordingPlatformUrl))
            return string.Empty;

        try
        {
            var client = _httpFactory.CreateClient("pipeline");

            // Build the transcript endpoint URL — follows the most common pattern across these platforms.
            // Verint:   GET {baseUrl}/speech-analytics/api/v1/calls/{id}/transcript
            // NICE CXOne: GET {baseUrl}/transcription/v1/contacts/{id}/transcript
            // Ozonetel: GET {baseUrl}/v1/calls/{id}/transcript?api_key={key}
            var interactionId = item.SourceReference ?? string.Empty;
            var endpoint = job.SourceType switch
            {
                "Verint" => $"{job.RecordingPlatformUrl.TrimEnd('/')}/speech-analytics/api/v1/calls/{interactionId}/transcript",
                "NICE" => $"{job.RecordingPlatformUrl.TrimEnd('/')}/transcription/v1/contacts/{interactionId}/transcript",
                "Ozonetel" => $"{job.RecordingPlatformUrl.TrimEnd('/')}/v1/calls/{interactionId}/transcript?api_key={job.RecordingPlatformApiKey}",
                _ => $"{job.RecordingPlatformUrl.TrimEnd('/')}/transcripts/{interactionId}"
            };

            var req = new HttpRequestMessage(HttpMethod.Get, endpoint);

            if (!string.IsNullOrEmpty(job.RecordingPlatformApiKey) && job.SourceType != "Ozonetel")
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", job.RecordingPlatformApiKey);

            if (!string.IsNullOrEmpty(job.RecordingPlatformTenantId))
                req.Headers.TryAddWithoutValidation("X-Tenant-Id", job.RecordingPlatformTenantId);

            using var resp = await client.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadAsStringAsync(ct);

            // Attempt to extract "text" field from JSON response if the platform returns JSON
            if (body.TrimStart().StartsWith('{') || body.TrimStart().StartsWith('['))
            {
                try
                {
                    var doc = System.Text.Json.JsonDocument.Parse(body);
                    // Try common field names used by these platforms
                    foreach (var key in new[] { "text", "transcript", "content", "transcriptText" })
                    {
                        if (doc.RootElement.TryGetProperty(key, out var prop) && prop.ValueKind == System.Text.Json.JsonValueKind.String)
                            return prop.GetString() ?? body;
                    }
                }
                catch { /* Fall back to raw body */ }
            }

            return body;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Platform} fetch failed for interaction {Id}", job.SourceType, item.SourceReference);
            throw;
        }
    }

    /// <summary>
    /// For connector-based jobs, lists available files/recordings and creates
    /// a CallPipelineItem for each one (items start as Pending — processing happens later).
    /// </summary>
    private async Task DiscoverConnectorItemsAsync(CallPipelineJob job, CancellationToken ct)
    {
        try
        {
            var refs = job.SourceType switch
            {
                "SFTP" => DiscoverSftpFiles(job),
                "SharePoint" => await DiscoverSharePointFilesAsync(job, ct),
                "Verint" or "NICE" or "Ozonetel" => await DiscoverRecordingPlatformCallsAsync(job, ct),
                _ => Enumerable.Empty<string>()
            };

            foreach (var reference in refs)
            {
                job.Items.Add(new CallPipelineItem
                {
                    SourceReference = reference,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Discovery failed for connector job {JobId} ({Type})", job.Id, job.SourceType);
            // Non-fatal: job can still be processed manually or with URLs supplied later
        }
    }

    private IEnumerable<string> DiscoverSftpFiles(CallPipelineJob job)
    {
        if (string.IsNullOrEmpty(job.SftpHost)) return Enumerable.Empty<string>();
        try
        {
            var port = job.SftpPort ?? 22;
            var path = job.SftpPath ?? "/";
            using var client = new Renci.SshNet.SftpClient(
                job.SftpHost, port,
                job.SftpUsername ?? string.Empty,
                job.SftpPassword ?? string.Empty);
            client.Connect();
            try
            {
                return client.ListDirectory(path)
                    .Where(f => !f.IsDirectory && (f.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                                                || f.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
                    .Select(f => f.FullName)
                    .ToList();
            }
            finally { client.Disconnect(); }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SFTP discovery failed for {Host}:{Path}", job.SftpHost, job.SftpPath);
            return Enumerable.Empty<string>();
        }
    }

    private async Task<IEnumerable<string>> DiscoverSharePointFilesAsync(CallPipelineJob job, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(job.SharePointSiteUrl)) return Enumerable.Empty<string>();
        try
        {
            var client = _httpFactory.CreateClient("pipeline");
            var library = job.SharePointLibraryName ?? "Shared Documents";
            var listUrl = $"{job.SharePointSiteUrl}/_api/web/lists/GetByTitle('{library}')/items?$select=FileLeafRef";

            var req = new HttpRequestMessage(HttpMethod.Get, listUrl);
            req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            using var resp = await client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return Enumerable.Empty<string>();

            var body = await resp.Content.ReadAsStringAsync(ct);
            var doc = System.Text.Json.JsonDocument.Parse(body);
            var results = new List<string>();
            if (doc.RootElement.TryGetProperty("value", out var values))
            {
                foreach (var item in values.EnumerateArray())
                {
                    if (item.TryGetProperty("FileLeafRef", out var name))
                        results.Add(name.GetString() ?? string.Empty);
                }
            }
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SharePoint discovery failed");
            return Enumerable.Empty<string>();
        }
    }

    private async Task<IEnumerable<string>> DiscoverRecordingPlatformCallsAsync(CallPipelineJob job, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(job.RecordingPlatformUrl)) return Enumerable.Empty<string>();
        try
        {
            var client = _httpFactory.CreateClient("pipeline");
            var now = DateTime.UtcNow;
            var from = job.FilterFromDate ?? now.AddDays(-1).ToString("yyyy-MM-dd");
            var to = job.FilterToDate ?? now.ToString("yyyy-MM-dd");

            var listEndpoint = job.SourceType switch
            {
                "Verint" => $"{job.RecordingPlatformUrl.TrimEnd('/')}/speech-analytics/api/v1/calls?startDate={from}&endDate={to}",
                "NICE" => $"{job.RecordingPlatformUrl.TrimEnd('/')}/transcription/v1/contacts?startTime={from}&endTime={to}",
                "Ozonetel" => $"{job.RecordingPlatformUrl.TrimEnd('/')}/v1/calls?from={from}&to={to}&api_key={job.RecordingPlatformApiKey}",
                _ => $"{job.RecordingPlatformUrl.TrimEnd('/')}/calls?from={from}&to={to}"
            };

            var req = new HttpRequestMessage(HttpMethod.Get, listEndpoint);
            if (!string.IsNullOrEmpty(job.RecordingPlatformApiKey) && job.SourceType != "Ozonetel")
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", job.RecordingPlatformApiKey);
            if (!string.IsNullOrEmpty(job.RecordingPlatformTenantId))
                req.Headers.TryAddWithoutValidation("X-Tenant-Id", job.RecordingPlatformTenantId);
            req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            using var resp = await client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return Enumerable.Empty<string>();

            var body = await resp.Content.ReadAsStringAsync(ct);
            var doc = System.Text.Json.JsonDocument.Parse(body);
            var ids = new List<string>();

            // Try common response shapes
            var root = doc.RootElement;
            var arr = root.ValueKind == System.Text.Json.JsonValueKind.Array
                ? root
                : root.TryGetProperty("calls", out var c) ? c
                : root.TryGetProperty("contacts", out var co) ? co
                : root.TryGetProperty("data", out var d) ? d
                : default;

            if (arr.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    foreach (var idKey in new[] { "id", "callId", "contactId", "interactionId" })
                    {
                        if (item.TryGetProperty(idKey, out var idProp))
                        {
                            ids.Add(idProp.ValueKind == System.Text.Json.JsonValueKind.String
                                ? idProp.GetString()!
                                : idProp.GetRawText());
                            break;
                        }
                    }
                }
            }

            return ids;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Platform} discovery failed", job.SourceType);
            return Enumerable.Empty<string>();
        }
    }

    // ── DTO mapping ───────────────────────────────────────────────────────────

    private Task<CallPipelineJobDto> MapJobToDto(CallPipelineJob job) =>
        Task.FromResult(MapJobToDtoSync(job));

    private static CallPipelineJobDto MapJobToDtoSync(CallPipelineJob job) => new()
    {
        Id = job.Id,
        Name = job.Name,
        SourceType = job.SourceType,
        FormId = job.FormId,
        FormName = job.Form?.Name,
        ProjectId = job.ProjectId,
        Status = job.Status,
        CreatedAt = job.CreatedAt,
        StartedAt = job.StartedAt,
        CompletedAt = job.CompletedAt,
        CreatedBy = job.CreatedBy,
        ErrorMessage = job.ErrorMessage,
        TotalItems = job.Items.Count,
        CompletedItems = job.Items.Count(i => i.Status == "Completed"),
        FailedItems = job.Items.Count(i => i.Status == "Failed"),
        Items = job.Items.OrderBy(i => i.Id).Select(i => new CallPipelineItemDto
        {
            Id = i.Id,
            JobId = i.JobId,
            SourceReference = i.SourceReference,
            AgentName = i.AgentName,
            CallReference = i.CallReference,
            CallDate = i.CallDate,
            Status = i.Status,
            CreatedAt = i.CreatedAt,
            ProcessedAt = i.ProcessedAt,
            ErrorMessage = i.ErrorMessage,
            EvaluationResultId = i.EvaluationResultId,
            ScorePercent = i.ScorePercent,
            AiReasoning = i.AiReasoning
        }).ToList()
    };
}
