using QAAutomation.API.DTOs;

namespace QAAutomation.API.Services;

/// <summary>Service that performs sentiment, emotion and recommendation analysis on a call transcript.</summary>
public interface ISentimentService
{
    /// <summary>
    /// Analyzes the transcript for overall sentiment, dominant emotions per speaker,
    /// key call moments, and generates coaching recommendations for the agent.
    /// </summary>
    Task<SentimentAnalysisResponseDto> AnalyzeAsync(
        SentimentAnalysisRequestDto request,
        CancellationToken cancellationToken = default);
}
