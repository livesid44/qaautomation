using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace QAAutomation.API.Services;

/// <summary>
/// Shared HTTP helper for calling the Google Gemini REST API.
/// Used by both <see cref="GoogleGeminiAutoAuditService"/> and <see cref="GoogleGeminiSentimentService"/>.
///
/// Endpoint: POST https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent
/// Reference: https://ai.google.dev/api/generate-content
/// </summary>
internal static class GeminiHttpHelper
{
    internal const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";

    /// <summary>
    /// Sends a request to the Gemini API and returns the generated text.
    /// </summary>
    /// <param name="httpFactory">Named <c>"gemini"</c> client factory.</param>
    /// <param name="apiKey">Google AI Studio API key.</param>
    /// <param name="model">Model name, e.g. <c>"gemini-1.5-pro"</c>.</param>
    /// <param name="systemPrompt">System instruction text.</param>
    /// <param name="userPrompt">User turn text.</param>
    /// <param name="temperature">Sampling temperature (0–2).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw text emitted by the model.</returns>
    public static async Task<string> CallAsync(
        IHttpClientFactory httpFactory,
        string apiKey,
        string model,
        string systemPrompt,
        string userPrompt,
        float temperature,
        CancellationToken cancellationToken)
    {
        var effectiveModel = string.IsNullOrWhiteSpace(model) ? "gemini-1.5-pro" : model;
        var url = $"{BaseUrl}{effectiveModel}:generateContent";

        var body = new
        {
            system_instruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = userPrompt } } }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = (double)temperature,
                maxOutputTokens = 4096
            }
        };

        var client = httpFactory.CreateClient("gemini");
        // x-goog-api-key is the preferred auth header for Google AI Studio API keys
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (client.DefaultRequestHeaders.Contains("x-goog-api-key"))
            client.DefaultRequestHeaders.Remove("x-goog-api-key");
        client.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);

        var content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8, "application/json");

        using var httpResponse = await client.PostAsync(url, content, cancellationToken);
        var responseBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Gemini API returned {(int)httpResponse.StatusCode}: {responseBody}");

        return ExtractText(responseBody);
    }

    /// <summary>
    /// Extracts the generated text from a Gemini API JSON response envelope.
    /// </summary>
    public static string ExtractText(string geminiJson)
    {
        using var doc = JsonDocument.Parse(geminiJson);
        var root = doc.RootElement;

        if (root.TryGetProperty("candidates", out var candidates) &&
            candidates.ValueKind == JsonValueKind.Array &&
            candidates.GetArrayLength() > 0)
        {
            var first = candidates[0];
            if (first.TryGetProperty("content", out var contentEl) &&
                contentEl.TryGetProperty("parts", out var parts) &&
                parts.ValueKind == JsonValueKind.Array &&
                parts.GetArrayLength() > 0 &&
                parts[0].TryGetProperty("text", out var textEl))
            {
                return textEl.GetString() ?? string.Empty;
            }
        }

        throw new InvalidOperationException(
            "Unexpected Gemini response structure — 'candidates[0].content.parts[0].text' not found.");
    }
}
