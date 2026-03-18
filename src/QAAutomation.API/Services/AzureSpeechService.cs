using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using QAAutomation.API.Models;

namespace QAAutomation.API.Services;

/// <summary>
/// Transcribes audio files to text using the Azure Cognitive Services
/// <b>Batch Transcription REST API</b> (v3.2).
///
/// Azure Speech Batch Transcription works asynchronously:
///   1. POST /speechtotext/v3.2/transcriptions  → starts job, returns 201 with Location header
///   2. Poll GET {location}                       → wait for "Succeeded" or "Failed"
///   3. GET {location}/files                      → list output files
///   4. Download the contentUrl of the "Transcription" file → extract display text
///
/// Reference: https://learn.microsoft.com/azure/ai-services/speech-service/batch-transcription-create-get
/// </summary>
public interface IAzureSpeechService
{
    /// <summary>
    /// Transcribes the audio file at <paramref name="audioUrl"/> to plain text.
    /// Returns null when the Speech service is not configured or an error occurs.
    /// </summary>
    Task<string?> TranscribeAudioUrlAsync(string audioUrl, CancellationToken ct = default);
}

public class AzureSpeechService : IAzureSpeechService
{
    private readonly IAiConfigService _aiConfig;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<AzureSpeechService> _logger;

    // Polling configuration
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(10);

    public AzureSpeechService(
        IAiConfigService aiConfig,
        IHttpClientFactory httpFactory,
        ILogger<AzureSpeechService> logger)
    {
        _aiConfig = aiConfig;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<string?> TranscribeAudioUrlAsync(string audioUrl, CancellationToken ct = default)
    {
        var cfg = await _aiConfig.GetAsync();
        if (string.IsNullOrWhiteSpace(cfg.SpeechEndpoint) || string.IsNullOrWhiteSpace(cfg.SpeechApiKey))
        {
            _logger.LogWarning("Azure Speech not configured — cannot transcribe audio at {Url}", audioUrl);
            return null;
        }

        try
        {
            var client = _httpFactory.CreateClient("speech");
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", cfg.SpeechApiKey);

            var baseUrl = BuildBaseUrl(cfg.SpeechEndpoint);

            // ── Step 1: Submit batch transcription job ──────────────────────
            var jobUrl = await SubmitJobAsync(client, baseUrl, audioUrl, ct);
            if (jobUrl is null)
            {
                _logger.LogWarning("Speech batch transcription submission failed for {Url}", audioUrl);
                return null;
            }

            // ── Step 2: Poll until the job completes ─────────────────────────
            var filesUrl = await PollUntilCompleteAsync(client, jobUrl, ct);
            if (filesUrl is null)
            {
                _logger.LogWarning("Speech transcription job did not complete in time for {Url}", audioUrl);
                return null;
            }

            // ── Step 3: Fetch the transcript text ────────────────────────────
            var transcript = await DownloadTranscriptAsync(client, filesUrl, ct);
            return transcript;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Speech transcription failed for {Url}", audioUrl);
            return null;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Normalises the configured value to a Speech API base URL.
    /// Accepts:
    ///   • A region code like "eastus"  → https://eastus.api.cognitive.microsoft.com
    ///   • A full endpoint URL
    /// </summary>
    private static string BuildBaseUrl(string endpointOrRegion)
    {
        endpointOrRegion = endpointOrRegion.Trim().TrimEnd('/');
        if (endpointOrRegion.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return endpointOrRegion;
        // Treat as region code
        return $"https://{endpointOrRegion}.api.cognitive.microsoft.com";
    }

    private async Task<string?> SubmitJobAsync(HttpClient client, string baseUrl, string audioUrl, CancellationToken ct)
    {
        var transcriptionsUrl = $"{baseUrl}/speechtotext/v3.2/transcriptions";

        var body = new
        {
            contentUrls = new[] { audioUrl },
            locale = "en-US",
            displayName = $"pipeline-{Guid.NewGuid():N}",
            properties = new
            {
                wordLevelTimestampsEnabled = false,
                punctuationMode = "DictatedAndAutomatic",
                profanityFilterMode = "None"
            }
        };

        var json = JsonSerializer.Serialize(body);
        using var req = new HttpRequestMessage(HttpMethod.Post, transcriptionsUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var resp = await client.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Speech submit failed {Status}: {Body}", resp.StatusCode, err);
            return null;
        }

        // Location header contains the URL to poll for status
        if (resp.Headers.Location is not null)
            return resp.Headers.Location.ToString();

        // Fall back: parse from response body
        var respBody = await resp.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(respBody);
        if (doc.RootElement.TryGetProperty("self", out var self))
            return self.GetString();

        return null;
    }

    private async Task<string?> PollUntilCompleteAsync(HttpClient client, string jobUrl, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + MaxWait;
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            await Task.Delay(PollInterval, ct);

            using var resp = await client.GetAsync(jobUrl, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var body = await resp.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(body);

            var status = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() : null;
            _logger.LogDebug("Speech transcription job status: {Status}", status);

            if (status == "Succeeded")
            {
                // Return the "files" link so we can fetch the transcript output
                if (doc.RootElement.TryGetProperty("links", out var links)
                    && links.TryGetProperty("files", out var filesLink))
                    return filesLink.GetString();
                // Fallback: files URL is conventionally jobUrl + "/files"
                return jobUrl.TrimEnd('/') + "/files";
            }

            if (status is "Failed" or "Deleted")
            {
                _logger.LogWarning("Speech transcription job ended with status {Status} at {Url}", status, jobUrl);
                return null;
            }
            // Otherwise: "NotStarted" or "Running" — continue polling
        }
        return null;
    }

    private async Task<string?> DownloadTranscriptAsync(HttpClient client, string filesUrl, CancellationToken ct)
    {
        using var resp = await client.GetAsync(filesUrl, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var body = await resp.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(body);

        // Find the first file with kind == "Transcription"
        string? contentUrl = null;
        if (doc.RootElement.TryGetProperty("values", out var values))
        {
            foreach (var file in values.EnumerateArray())
            {
                var kind = file.TryGetProperty("kind", out var k) ? k.GetString() : null;
                if (kind == "Transcription" && file.TryGetProperty("links", out var links)
                    && links.TryGetProperty("contentUrl", out var cu))
                {
                    contentUrl = cu.GetString();
                    break;
                }
            }
        }

        if (contentUrl is null) return null;

        // Download the transcription result JSON
        var resultJson = await client.GetStringAsync(contentUrl, ct);
        return ExtractDisplayText(resultJson);
    }

    /// <summary>
    /// Extracts all display-form words from the Azure Speech batch result JSON.
    /// The result format is:
    /// { combinedRecognizedPhrases: [ { display: "..." } ] }
    /// or per-phrase:
    /// { recognizedPhrases: [ { nBest: [ { display: "..." } ] } ] }
    /// </summary>
    internal static string? ExtractDisplayText(string resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson)) return null;
        try
        {
            var doc = JsonDocument.Parse(resultJson);
            var sb = new StringBuilder();

            // Prefer combinedRecognizedPhrases — already has diarization + full text
            if (doc.RootElement.TryGetProperty("combinedRecognizedPhrases", out var combined))
            {
                foreach (var phrase in combined.EnumerateArray())
                {
                    if (phrase.TryGetProperty("display", out var d))
                    {
                        var text = d.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            if (sb.Length > 0) sb.Append(' ');
                            sb.Append(text);
                        }
                    }
                }
            }

            if (sb.Length > 0) return sb.ToString();

            // Fallback: stitch together nBest[0].display from each recognizedPhrase
            if (doc.RootElement.TryGetProperty("recognizedPhrases", out var phrases))
            {
                foreach (var phrase in phrases.EnumerateArray())
                {
                    if (phrase.TryGetProperty("nBest", out var nBest))
                    {
                        var best = nBest.EnumerateArray().FirstOrDefault();
                        if (best.TryGetProperty("display", out var d))
                        {
                            var text = d.GetString();
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                if (sb.Length > 0) sb.Append(' ');
                                sb.Append(text);
                            }
                        }
                    }
                }
            }

            return sb.Length > 0 ? sb.ToString() : null;
        }
        catch (Exception ex)
        {
            return $"[Transcription parse error: {ex.Message}]";
        }
    }
}

/// <summary>
/// No-op speech service used when Azure Speech is not configured.
/// Returns null to signal the pipeline item should be marked Failed with a clear message.
/// </summary>
public class MockAzureSpeechService : IAzureSpeechService
{
    private readonly ILogger<MockAzureSpeechService> _logger;

    public MockAzureSpeechService(ILogger<MockAzureSpeechService> logger) => _logger = logger;

    public Task<string?> TranscribeAudioUrlAsync(string audioUrl, CancellationToken ct = default)
    {
        _logger.LogInformation("MockAzureSpeechService: Speech not configured — cannot transcribe {Url}", audioUrl);
        return Task.FromResult<string?>(null);
    }
}

/// <summary>Runtime selector — returns MockAzureSpeechService when SpeechEndpoint is blank.</summary>
public class RuntimeSpeechTranscriptionService : IAzureSpeechService
{
    private readonly IAiConfigService _aiConfig;
    private readonly AzureSpeechService _real;
    private readonly MockAzureSpeechService _mock;

    public RuntimeSpeechTranscriptionService(
        IAiConfigService aiConfig,
        AzureSpeechService real,
        MockAzureSpeechService mock)
    {
        _aiConfig = aiConfig;
        _real = real;
        _mock = mock;
    }

    public async Task<string?> TranscribeAudioUrlAsync(string audioUrl, CancellationToken ct = default)
    {
        var cfg = await _aiConfig.GetAsync();
        return !string.IsNullOrWhiteSpace(cfg.SpeechEndpoint) && !string.IsNullOrWhiteSpace(cfg.SpeechApiKey)
            ? await _real.TranscribeAudioUrlAsync(audioUrl, ct)
            : await _mock.TranscribeAudioUrlAsync(audioUrl, ct);
    }
}
