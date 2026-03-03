using Microsoft.AspNetCore.Mvc;
using QAAutomation.API.DTOs;
using QAAutomation.API.Services;

namespace QAAutomation.API.Controllers;

/// <summary>
/// Accepts free-text questions and returns tenant-scoped query results together
/// with AI-generated insights. Uses the tenant's configured LLM subscription
/// (AiConfig) for both NL→SQL translation and narrative insights.
///
/// This endpoint is called server-side only (Web → API proxy); it does not use
/// cookie-based authentication, so CSRF token validation is not applicable.
/// </summary>
[ApiController]
[IgnoreAntiforgeryToken]
[Route("api/[controller]")]
public class InsightsChatController : ControllerBase
{
    private readonly InsightsChatService _svc;

    public InsightsChatController(InsightsChatService svc) => _svc = svc;

    /// <summary>
    /// Ask a free-text question. Returns SQL, result rows, column names, and AI insights.
    /// All data is scoped to the specified ProjectId (tenant).
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    [ProducesResponseType(typeof(InsightsChatResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InsightsChatResponseDto>> Ask(
        [FromBody] InsightsChatRequestDto req,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(req.Question))
            return BadRequest(new { error = "Question is required." });

        var result = await _svc.AskAsync(req, cancellationToken);
        return Ok(result);
    }
}
