using Microsoft.AspNetCore.Mvc;
using QAAutomation.API.DTOs;
using QAAutomation.API.Services;

namespace QAAutomation.API.Controllers;

/// <summary>Controller for sentiment, emotion and coaching-recommendation analysis of call transcripts.</summary>
[ApiController]
[Route("api/[controller]")]
public class SentimentController : ControllerBase
{
    private readonly ISentimentService _sentimentService;

    public SentimentController(ISentimentService sentimentService)
    {
        _sentimentService = sentimentService;
    }

    /// <summary>
    /// Analyzes a call transcript for overall sentiment, dominant emotions, key moments,
    /// and generates concrete coaching recommendations for the agent.
    /// </summary>
    /// <param name="request">The sentiment analysis request containing the transcript.</param>
    /// <returns>Sentiment analysis result including emotions, key moments and recommendations.</returns>
    [HttpPost("analyze")]
    [ProducesResponseType(typeof(SentimentAnalysisResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SentimentAnalysisResponseDto>> Analyze([FromBody] SentimentAnalysisRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Transcript))
            return BadRequest("Transcript cannot be empty.");

        var result = await _sentimentService.AnalyzeAsync(request, CancellationToken.None);
        return Ok(result);
    }
}
