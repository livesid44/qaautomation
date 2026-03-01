using Microsoft.AspNetCore.Mvc;
using QAAutomation.API.DTOs;
using QAAutomation.API.Services;
using QAAutomation.API.Models;

namespace QAAutomation.API.Controllers;

/// <summary>Controller for reading and updating AI configuration stored in the database.</summary>
[ApiController]
[Route("api/[controller]")]
public class AiConfigController : ControllerBase
{
    private readonly IAiConfigService _svc;

    public AiConfigController(IAiConfigService svc) => _svc = svc;

    /// <summary>Get the current AI configuration.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(AiConfigDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AiConfigDto>> Get()
    {
        var cfg = await _svc.GetAsync();
        return Ok(ToDto(cfg));
    }

    /// <summary>Save updated AI configuration to the database.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(AiConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AiConfigDto>> Put([FromBody] AiConfigDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.LlmProvider))
            return BadRequest("LlmProvider is required.");
        var cfg = await _svc.SaveAsync(dto);
        return Ok(ToDto(cfg));
    }

    private static AiConfigDto ToDto(AiConfig cfg) => new()
    {
        LlmProvider = cfg.LlmProvider,
        LlmEndpoint = cfg.LlmEndpoint,
        LlmApiKey = string.IsNullOrEmpty(cfg.LlmApiKey) ? "" : "***",  // mask key in responses
        LlmDeployment = cfg.LlmDeployment,
        LlmTemperature = cfg.LlmTemperature,
        SentimentProvider = cfg.SentimentProvider,
        LanguageEndpoint = cfg.LanguageEndpoint,
        LanguageApiKey = string.IsNullOrEmpty(cfg.LanguageApiKey) ? "" : "***",
        RagTopK = cfg.RagTopK,
        UpdatedAt = cfg.UpdatedAt
    };
}
