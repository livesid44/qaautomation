namespace QAAutomation.API.Models;

/// <summary>
/// Singleton row (Id=1) that stores all AI configuration in the database.
/// Users edit this via the AI Settings UI rather than appsettings.json.
/// </summary>
public class AiConfig
{
    public int Id { get; set; } = 1;

    // ── LLM (Quality Scoring) ────────────────────────────────────────────────
    /// <summary>"AzureOpenAI", "OpenAI", or "Google"</summary>
    public string LlmProvider { get; set; } = "AzureOpenAI";
    public string LlmEndpoint { get; set; } = string.Empty;
    public string LlmApiKey { get; set; } = string.Empty;
    public string LlmDeployment { get; set; } = "gpt-4o";
    public float LlmTemperature { get; set; } = 0.1f;

    // ── Sentiment / Emotion Analysis ─────────────────────────────────────────
    /// <summary>"AzureOpenAI", "OpenAI", "AzureLanguage", or "Google"</summary>
    public string SentimentProvider { get; set; } = "AzureOpenAI";
    /// <summary>Only used when SentimentProvider = "AzureLanguage"</summary>
    public string LanguageEndpoint { get; set; } = string.Empty;
    /// <summary>Only used when SentimentProvider = "AzureLanguage"</summary>
    public string LanguageApiKey { get; set; } = string.Empty;

    // ── RAG ──────────────────────────────────────────────────────────────────
    /// <summary>Number of top KB chunks to inject per field.</summary>
    public int RagTopK { get; set; } = 3;

    // ── Google (Gemini LLM + Cloud Speech-to-Text) ──────────────────────────
    /// <summary>
    /// Google AI Studio API key.  Used when <see cref="LlmProvider"/> = "Google",
    /// <see cref="SentimentProvider"/> = "Google", or <see cref="SpeechProvider"/> = "Google".
    /// Obtain from https://aistudio.google.com/app/apikey
    /// </summary>
    public string GoogleApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Google Gemini model name used for LLM and sentiment analysis.
    /// Examples: "gemini-1.5-pro", "gemini-2.0-flash", "gemini-1.5-flash".
    /// </summary>
    public string GoogleGeminiModel { get; set; } = "gemini-1.5-pro";

    // ── Speech-to-Text ───────────────────────────────────────────────────────
    /// <summary>"Azure" (default) or "Google"</summary>
    public string SpeechProvider { get; set; } = "Azure";

    // ── Azure Speech-to-Text ─────────────────────────────────────────────────
    /// <summary>
    /// Azure Speech Service region (e.g. "eastus") or full custom endpoint URL.
    /// Used by the call pipeline to transcribe audio recordings before QA scoring.
    /// Leave empty to skip audio transcription (audio items will fail gracefully).
    /// </summary>
    public string SpeechEndpoint { get; set; } = string.Empty;
    /// <summary>Azure Speech Service subscription key (Ocp-Apim-Subscription-Key).</summary>
    public string SpeechApiKey { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
