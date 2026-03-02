namespace QAAutomation.API.Services;

/// <summary>
/// Helpers for detecting whether a URL or HTTP Content-Type indicates audio content
/// versus a plain-text or JSON transcript.
/// </summary>
public static class AudioFormatHelper
{
    /// <summary>
    /// Audio file extensions that Azure Speech Batch Transcription supports.
    /// See https://learn.microsoft.com/azure/ai-services/speech-service/batch-transcription-audio-data
    /// </summary>
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".mp3", ".ogg", ".flac", ".m4a", ".mp4", ".wma", ".aac", ".opus"
    };

    /// <summary>
    /// MIME type prefixes / values that represent audio or video (which contains audio).
    /// </summary>
    private static readonly HashSet<string> AudioMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio/wav", "audio/wave", "audio/x-wav",
        "audio/mpeg", "audio/mp3",
        "audio/ogg", "audio/vorbis",
        "audio/flac", "audio/x-flac",
        "audio/aac", "audio/x-aac",
        "audio/mp4", "audio/x-m4a",
        "audio/opus",
        "audio/wma", "audio/x-ms-wma",
        "video/mp4", "video/webm", "video/x-msvideo"
    };

    /// <summary>
    /// Returns true when the URL path ends with a known audio extension.
    /// Also accepts URLs that contain audio file extensions before query strings.
    /// </summary>
    public static bool IsAudioUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        try
        {
            // Strip query string and fragment before checking extension
            var path = new Uri(url, UriKind.Absolute).AbsolutePath;
            var ext = Path.GetExtension(path);
            return AudioExtensions.Contains(ext);
        }
        catch
        {
            // Fallback: check the raw string after removing query part
            var pathPart = url.Split('?')[0].Split('#')[0];
            return AudioExtensions.Contains(Path.GetExtension(pathPart));
        }
    }

    /// <summary>
    /// Returns true when the HTTP Content-Type header indicates audio or video media.
    /// </summary>
    public static bool IsAudioContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return false;
        // Normalise: strip parameters like "; charset=utf-8"
        var mime = contentType.Split(';')[0].Trim().ToLowerInvariant();
        return AudioMimeTypes.Contains(mime) || mime.StartsWith("audio/") || mime.StartsWith("video/");
    }

    /// <summary>
    /// Returns true when the URL path ends with a known audio extension OR the HTTP
    /// response Content-Type indicates audio/video media.
    /// </summary>
    public static bool IsAudio(string url, string? contentType = null) =>
        IsAudioUrl(url) || IsAudioContentType(contentType);
}
