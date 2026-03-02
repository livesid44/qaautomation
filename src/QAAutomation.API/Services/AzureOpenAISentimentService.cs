using System.Text;
using System.Text.Json;
using OpenAI.Chat;
using QAAutomation.API.DTOs;

namespace QAAutomation.API.Services;

/// <summary>
/// Analyzes call transcripts for sentiment, emotion and recommendations using Azure OpenAI.
/// Configuration is read from the database (AiConfig) rather than appsettings.
/// </summary>
public class AzureOpenAISentimentService : ISentimentService
{
    private readonly IAiConfigService _aiConfig;
    private readonly ILogger<AzureOpenAISentimentService> _logger;

    public AzureOpenAISentimentService(IAiConfigService aiConfig, ILogger<AzureOpenAISentimentService> logger)
    {
        _aiConfig = aiConfig;
        _logger = logger;
    }

    public async Task<SentimentAnalysisResponseDto> AnalyzeAsync(
        SentimentAnalysisRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var cfg = await _aiConfig.GetAsync();
        var apiKey = cfg.LlmApiKey;
        var (endpoint, deployment) = AzureOpenAIHelper.NormalizeEndpoint(cfg.LlmEndpoint, cfg.LlmDeployment);

        var response = new SentimentAnalysisResponseDto { IsAiGenerated = true };

        try
        {
            var chatClient = AzureOpenAIHelper.CreateClient(endpoint, apiKey, deployment);

            var options = new ChatCompletionOptions
            {
                Temperature = 0.2f,
                MaxOutputTokenCount = 2048,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            var completion = await chatClient.CompleteChatAsync(
                new List<ChatMessage> { new SystemChatMessage(BuildSystemPrompt()), new UserChatMessage(BuildUserPrompt(request.Transcript)) },
                options, cancellationToken);

            ParseLlmResponse(completion.Value.Content[0].Text, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure OpenAI sentiment analysis failed");
            response.AnalysisError = $"Azure OpenAI analysis failed: {ex.Message}";
            response.IsAiGenerated = false;
            FillDefaults(response);
        }

        return response;
    }

    private static string BuildSystemPrompt() => """
        You are an expert in customer service call quality analysis specializing in sentiment, emotion, and coaching.
        Analyze the provided call transcript and return ONLY a valid JSON object with exactly this structure:
        {
          "overallSentiment": "Positive"|"Neutral"|"Negative",
          "overallScore": <0-100 integer>,
          "agentSentiment": "Positive"|"Neutral"|"Negative",
          "agentScore": <0-100 integer>,
          "customerSentiment": "Positive"|"Neutral"|"Negative",
          "customerScore": <0-100 integer>,
          "sentimentTrend": "Improving"|"Stable"|"Declining",
          "dominantEmotions": [
            { "emotion": "<label>", "confidence": <0-100 integer>, "speaker": "Agent"|"Customer" }
          ],
          "keyMoments": [
            { "title": "<short title>", "sentiment": "Positive"|"Neutral"|"Negative", "excerpt": "<brief quote or paraphrase>" }
          ],
          "recommendations": [
            { "category": "<category>", "priority": "High"|"Medium"|"Low", "text": "<actionable recommendation>", "evidence": "<what in the transcript supports this>" }
          ],
          "overallInsight": "<2-3 sentence paragraph summarizing the interaction quality, sentiment arc, and key coaching opportunity>"
        }

        RULES:
        - overallScore, agentScore, customerScore: 0=very negative, 50=neutral, 100=very positive
        - Include 3-5 dominantEmotions covering both agent and customer
        - Include 3-5 keyMoments that represent turning points, escalations, resolutions, or compliance checkpoints
        - Include 3-5 recommendations that are specific, actionable, and evidence-based
        - sentimentTrend: how does the customer's sentiment change from start to end of the call?
        """;

    private static string BuildUserPrompt(string transcript) =>
        $"Analyze this call transcript:\n\n--- TRANSCRIPT ---\n{transcript}\n--- END ---\n\nReturn JSON only.";

    private static void ParseLlmResponse(string json, SentimentAnalysisResponseDto response)
    {
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("overallSentiment", out var el)) response.OverallSentiment = el.GetString() ?? "";
        if (root.TryGetProperty("overallScore", out el)) response.OverallScore = el.GetDouble();
        if (root.TryGetProperty("agentSentiment", out el)) response.AgentSentiment = el.GetString() ?? "";
        if (root.TryGetProperty("agentScore", out el)) response.AgentScore = el.GetDouble();
        if (root.TryGetProperty("customerSentiment", out el)) response.CustomerSentiment = el.GetString() ?? "";
        if (root.TryGetProperty("customerScore", out el)) response.CustomerScore = el.GetDouble();
        if (root.TryGetProperty("sentimentTrend", out el)) response.SentimentTrend = el.GetString() ?? "";
        if (root.TryGetProperty("overallInsight", out el)) response.OverallInsight = el.GetString() ?? "";

        if (root.TryGetProperty("dominantEmotions", out var emotionsEl) && emotionsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in emotionsEl.EnumerateArray())
            {
                response.DominantEmotions.Add(new DetectedEmotionDto
                {
                    Emotion = e.TryGetProperty("emotion", out var v) ? v.GetString() ?? "" : "",
                    Confidence = e.TryGetProperty("confidence", out v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0,
                    Speaker = e.TryGetProperty("speaker", out v) ? v.GetString() ?? "" : ""
                });
            }
        }

        if (root.TryGetProperty("keyMoments", out var momentsEl) && momentsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var m in momentsEl.EnumerateArray())
            {
                response.KeyMoments.Add(new KeyMomentDto
                {
                    Title = m.TryGetProperty("title", out var v) ? v.GetString() ?? "" : "",
                    Sentiment = m.TryGetProperty("sentiment", out v) ? v.GetString() ?? "" : "",
                    Excerpt = m.TryGetProperty("excerpt", out v) ? v.GetString() ?? "" : ""
                });
            }
        }

        if (root.TryGetProperty("recommendations", out var recsEl) && recsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var r in recsEl.EnumerateArray())
            {
                response.Recommendations.Add(new CoachingRecommendationDto
                {
                    Category = r.TryGetProperty("category", out var v) ? v.GetString() ?? "" : "",
                    Priority = r.TryGetProperty("priority", out v) ? v.GetString() ?? "" : "",
                    Text = r.TryGetProperty("text", out v) ? v.GetString() ?? "" : "",
                    Evidence = r.TryGetProperty("evidence", out v) ? v.GetString() ?? "" : ""
                });
            }
        }
    }

    private static void FillDefaults(SentimentAnalysisResponseDto response)
    {
        response.OverallSentiment = "Neutral";
        response.OverallScore = 50;
        response.AgentSentiment = "Neutral";
        response.AgentScore = 50;
        response.CustomerSentiment = "Neutral";
        response.CustomerScore = 50;
        response.SentimentTrend = "Stable";
        response.OverallInsight = "Sentiment analysis was not available. Please review the transcript manually.";
    }
}
