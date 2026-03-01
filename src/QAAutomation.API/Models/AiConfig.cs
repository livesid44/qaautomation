namespace QAAutomation.API.Models;

/// <summary>
/// Singleton row (Id=1) that stores all AI configuration in the database.
/// Users edit this via the AI Settings UI rather than appsettings.json.
/// </summary>
public class AiConfig
{
    public int Id { get; set; } = 1;

    // ── LLM (Quality Scoring) ────────────────────────────────────────────────
    /// <summary>"AzureOpenAI" or "OpenAI"</summary>
    public string LlmProvider { get; set; } = "AzureOpenAI";
    public string LlmEndpoint { get; set; } = string.Empty;
    public string LlmApiKey { get; set; } = string.Empty;
    public string LlmDeployment { get; set; } = "gpt-4o";
    public float LlmTemperature { get; set; } = 0.1f;

    // ── Sentiment / Emotion Analysis ─────────────────────────────────────────
    /// <summary>"AzureOpenAI", "OpenAI", or "AzureLanguage"</summary>
    public string SentimentProvider { get; set; } = "AzureOpenAI";
    /// <summary>Only used when SentimentProvider = "AzureLanguage"</summary>
    public string LanguageEndpoint { get; set; } = string.Empty;
    /// <summary>Only used when SentimentProvider = "AzureLanguage"</summary>
    public string LanguageApiKey { get; set; } = string.Empty;

    // ── RAG ──────────────────────────────────────────────────────────────────
    /// <summary>Number of top KB chunks to inject per field.</summary>
    public int RagTopK { get; set; } = 3;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
