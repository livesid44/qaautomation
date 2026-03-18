using QAAutomation.API.DTOs;

namespace QAAutomation.API.Services;

/// <summary>
/// Mock sentiment service used when Azure OpenAI is not configured.
/// Returns realistic simulated sentiment / emotion / recommendation data.
/// </summary>
public class MockSentimentService : ISentimentService
{
    private readonly ILogger<MockSentimentService> _logger;

    public MockSentimentService(ILogger<MockSentimentService> logger)
    {
        _logger = logger;
    }

    public Task<SentimentAnalysisResponseDto> AnalyzeAsync(
        SentimentAnalysisRequestDto request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MockSentimentService: Azure OpenAI not configured — returning simulated sentiment");

        var rng = new Random(request.Transcript.Length * 3 + 7);

        // Generate a plausible result for a typical customer support call
        var customerStartScore = rng.Next(25, 55);    // Customer starts somewhat frustrated
        var customerEndScore = rng.Next(65, 90);      // Customer ends happy
        var overallScore = (customerStartScore + customerEndScore) / 2 + rng.Next(5, 15);
        var agentScore = rng.Next(72, 92);

        var agentEmotions = new[] { "Empathy", "Professionalism", "Patience", "Confidence", "Attentiveness" };
        var customerEmotions = new[] { "Frustration", "Concern", "Relief", "Satisfaction", "Gratitude" };

        var emotions = new List<DetectedEmotionDto>
        {
            new() { Emotion = agentEmotions[rng.Next(agentEmotions.Length)],   Confidence = rng.Next(75, 96), Speaker = "Agent" },
            new() { Emotion = agentEmotions[rng.Next(agentEmotions.Length)],   Confidence = rng.Next(68, 92), Speaker = "Agent" },
            new() { Emotion = customerEmotions[rng.Next(customerEmotions.Length)], Confidence = rng.Next(70, 95), Speaker = "Customer" },
            new() { Emotion = customerEmotions[rng.Next(customerEmotions.Length)], Confidence = rng.Next(65, 88), Speaker = "Customer" },
            new() { Emotion = agentEmotions[rng.Next(agentEmotions.Length)],   Confidence = rng.Next(60, 85), Speaker = "Agent" }
        };
        // Deduplicate emotion labels
        emotions = emotions.GroupBy(e => e.Emotion + e.Speaker).Select(g => g.First()).ToList();

        var moments = new List<KeyMomentDto>
        {
            new() { Title = "Call Opening",            Sentiment = "Neutral",   Excerpt = "[SIMULATED] Agent greeted the customer professionally and offered assistance." },
            new() { Title = "Customer Concern Raised", Sentiment = "Negative",  Excerpt = "[SIMULATED] Customer expressed concern about an issue — agent acknowledged promptly." },
            new() { Title = "Identity Verification",   Sentiment = "Neutral",   Excerpt = "[SIMULATED] Agent followed security procedures to verify the customer's identity." },
            new() { Title = "Issue Resolution",        Sentiment = "Positive",  Excerpt = "[SIMULATED] Agent provided a clear resolution and outlined the next steps." },
            new() { Title = "Call Closing",            Sentiment = "Positive",  Excerpt = "[SIMULATED] Customer expressed satisfaction; agent delivered a professional sign-off." }
        };

        var recommendations = new List<CoachingRecommendationDto>
        {
            new()
            {
                Category = "Empathy",
                Priority = "High",
                Text = "[SIMULATED] Use explicit empathy statements earlier in the call when the customer first raises a concern — e.g. 'I completely understand how frustrating this must be.'",
                Evidence = "The agent acknowledged the issue but delayed empathy language until after identity verification."
            },
            new()
            {
                Category = "Communication",
                Priority = "Medium",
                Text = "[SIMULATED] Provide proactive time estimates at each step — customers feel more at ease when they know exactly how long each process takes.",
                Evidence = "The agent gave timelines for the resolution but not for the verification steps."
            },
            new()
            {
                Category = "Product Knowledge",
                Priority = "Medium",
                Text = "[SIMULATED] Reinforce the company's fraud/dispute protection policy proactively — customers should hear this before they ask.",
                Evidence = "The customer asked about the investigation timeline rather than the agent offering this information first."
            },
            new()
            {
                Category = "Call Control",
                Priority = "Low",
                Text = "[SIMULATED] Practice brief, confident transition phrases between steps to avoid dead air (e.g. 'Let me pull that up for you right now').",
                Evidence = "There were minor pauses between the verification step and pulling up the account details."
            }
        };

        var response = new SentimentAnalysisResponseDto
        {
            OverallSentiment = overallScore >= 70 ? "Positive" : overallScore >= 45 ? "Neutral" : "Negative",
            OverallScore = overallScore,
            AgentSentiment = "Positive",
            AgentScore = agentScore,
            CustomerSentiment = customerEndScore >= 65 ? "Positive" : "Neutral",
            CustomerScore = customerEndScore,
            SentimentTrend = "Improving",
            DominantEmotions = emotions,
            KeyMoments = moments,
            Recommendations = recommendations,
            OverallInsight = $"[SIMULATED — Azure OpenAI not configured] This call shows a positive interaction arc. " +
                             $"The customer started with moderate concern (score: {customerStartScore}/100) and ended satisfied (score: {customerEndScore}/100). " +
                             "The agent demonstrated professionalism throughout. Connect Azure OpenAI for real AI-powered sentiment analysis.",
            IsAiGenerated = false
        };

        return Task.FromResult(response);
    }
}
