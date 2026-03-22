using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace QAAutomation.API.Services;

/// <summary>
/// Transcribes audio files to text using the Google Cloud Speech-to-Text REST API.
///
/// Strategy:
///   1. Download the audio file from the provided HTTPS URL.
///   2. If the audio is within the 10 MB inline limit, submit it as base64-encoded content
///      using the Long Running Recognize endpoint, which supports longer calls.
///   3. Poll until the operation completes (up to <see cref="MaxWait"/>).
///   4. Extract and return the transcript text.
///
/// Requirements:
///   • A Google API key with the Cloud Speech-to-Text API enabled.
///   • The audio must be accessible over HTTPS.
///   • Audio > 10 MB must be hosted in Google Cloud Storage (not currently supported
///     by this service — a warning is logged and null is returned in that case).
///
/// Reference: https://cloud.google.com/speech-to-text/docs/async-recognize
/// </summary>
public class GoogleSpeechService : IAzureSpeechService
{
    private const string LongRunningUrl = "https://speech.googleapis.com/v1/speech:longrunningrecognize";
    private const string OperationsUrl   = "https://speech.googleapis.com/v1/operations/";
    private const long   MaxInlineBytes  = 10 * 1024 * 1024; // 10 MB Google limit for inline audio

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxWait       = TimeSpan.FromMinutes(10);

    private readonly IAiConfigService _aiConfig;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<GoogleSpeechService> _logger;

    public GoogleSpeechService(
        IAiConfigService aiConfig,
        IHttpClientFactory httpFactory,
        ILogger<GoogleSpeechService> logger)
    {
        _aiConfig = aiConfig;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string?> TranscribeAudioUrlAsync(string audioUrl, CancellationToken ct = default)
    {
        var cfg = await _aiConfig.GetAsync();

        if (string.IsNullOrWhiteSpace(cfg.GoogleApiKey))
        {
            _logger.LogWarning("Google Speech-to-Text: GoogleApiKey is not configured — cannot transcribe {Url}", audioUrl);
            return null;
        }

        try
        {
            // ── Step 1: Download audio ────────────────────────────────────────
            byte[] audioBytes;
            try
            {
                var downloadClient = _httpFactory.CreateClient("pipeline");
                audioBytes = await downloadClient.GetByteArrayAsync(audioUrl, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Google STT: failed to download audio from {Url}", audioUrl);
                return null;
            }

            if (audioBytes.Length > MaxInlineBytes)
            {
                _logger.LogWarning(
                    "Google STT: audio file at {Url} is {Mb:F1} MB which exceeds the 10 MB inline limit. " +
                    "Host the file in Google Cloud Storage and configure a GCS URI for longer recordings.",
                    audioUrl, audioBytes.Length / (1024.0 * 1024.0));
                return null;
            }

            var audioBase64 = Convert.ToBase64String(audioBytes);
            var encoding    = DetectEncoding(audioUrl);

            // ── Step 2: Submit long-running recognition job ───────────────────
            var operationName = await SubmitJobAsync(cfg.GoogleApiKey, audioBase64, encoding, ct);
            if (operationName is null)
            {
                _logger.LogWarning("Google STT: failed to start recognition job for {Url}", audioUrl);
                return null;
            }

            // ── Step 3: Poll until done ───────────────────────────────────────
            return await PollOperationAsync(cfg.GoogleApiKey, operationName, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google STT: unexpected error transcribing {Url}", audioUrl);
            return null;
        }
    }

    private async Task<string?> SubmitJobAsync(
        string apiKey, string audioBase64, string encoding, CancellationToken ct)
    {
        var requestBody = new
        {
            config = new
            {
                encoding,
                languageCode = "en-US",
                enableAutomaticPunctuation = true,
                model = "latest_long",           // best accuracy for call-center audio
                useEnhanced = true
            },
            audio = new { content = audioBase64 }
        };

        var client = _httpFactory.CreateClient("speech");
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var url = $"{LongRunningUrl}?key={Uri.EscapeDataString(apiKey)}";
        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(url, content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Google STT submit job failed {Status}: {Body}", (int)response.StatusCode, body);
            return null;
        }

        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("name", out var nameEl))
            return nameEl.GetString();

        _logger.LogWarning("Google STT submit job: 'name' not found in response: {Body}", body);
        return null;
    }

    private async Task<string?> PollOperationAsync(string apiKey, string operationName, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient("speech");
        var url = $"{OperationsUrl}{Uri.EscapeDataString(operationName)}?key={Uri.EscapeDataString(apiKey)}";

        var deadline = DateTime.UtcNow.Add(MaxWait);

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(PollInterval, ct);

            using var resp = await client.GetAsync(url, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google STT poll failed {Status}: {Body}", (int)resp.StatusCode, body);
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // done = true once the operation has finished
            if (!root.TryGetProperty("done", out var doneEl) || !doneEl.GetBoolean())
                continue;

            // Check for error
            if (root.TryGetProperty("error", out var errEl))
            {
                var errMsg = errEl.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : body;
                _logger.LogWarning("Google STT operation failed: {Error}", errMsg);
                return null;
            }

            // Extract transcript text from response.results[].alternatives[0].transcript
            return ExtractTranscript(root);
        }

        _logger.LogWarning("Google STT: operation '{Op}' did not complete within {Min} minutes",
            operationName, MaxWait.TotalMinutes);
        return null;
    }

    private static string? ExtractTranscript(JsonElement root)
    {
        if (!root.TryGetProperty("response", out var responseEl)) return null;
        if (!responseEl.TryGetProperty("results", out var resultsEl)) return null;
        if (resultsEl.ValueKind != JsonValueKind.Array) return null;

        var sb = new StringBuilder();
        foreach (var result in resultsEl.EnumerateArray())
        {
            if (!result.TryGetProperty("alternatives", out var alts)) continue;
            if (alts.ValueKind != JsonValueKind.Array || alts.GetArrayLength() == 0) continue;
            if (alts[0].TryGetProperty("transcript", out var tEl))
                sb.AppendLine(tEl.GetString());
        }

        return sb.Length > 0 ? sb.ToString().Trim() : null;
    }

    /// <summary>
    /// Infers the Google STT audio encoding from the file extension in the URL.
    /// Note: M4A and AAC are not natively supported by Google STT — if possible,
    /// convert them to MP3 or FLAC before transcribing.
    /// </summary>
    private string DetectEncoding(string url)
    {
        var path = url.Split('?')[0].ToLowerInvariant();
        if (path.EndsWith(".mp3"))  return "MP3";
        if (path.EndsWith(".ogg"))  return "OGG_OPUS";
        if (path.EndsWith(".opus")) return "OGG_OPUS";
        if (path.EndsWith(".flac")) return "FLAC";
        if (path.EndsWith(".m4a") || path.EndsWith(".aac"))
        {
            _logger.LogWarning(
                "Google STT: AAC/M4A audio ({Url}) is not natively supported by Google Cloud STT. " +
                "Attempting MP3 encoding — transcription may be inaccurate. " +
                "Pre-converting to MP3 or FLAC is recommended.", url);
            return "MP3";
        }
        if (!path.EndsWith(".wav"))
        {
            _logger.LogWarning(
                "Google STT: unknown audio format for {Url}. Defaulting to LINEAR16 (WAV PCM) encoding. " +
                "If transcription fails, ensure the file is PCM WAV or convert to a supported format (MP3, FLAC, OGG_OPUS).", url);
        }
        // LINEAR16 requires uncompressed PCM audio (standard WAV)
        return "LINEAR16";
    }
}
