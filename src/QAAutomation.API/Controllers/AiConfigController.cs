using Microsoft.AspNetCore.Mvc;
using OpenAI.Chat;
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
    private readonly IHttpClientFactory _httpFactory;

    public AiConfigController(IAiConfigService svc, IHttpClientFactory httpFactory)
    {
        _svc = svc;
        _httpFactory = httpFactory;
    }

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

    /// <summary>
    /// Sends a simple "hi" message to the configured LLM and returns the response,
    /// confirming end-to-end connectivity and correct credentials.
    /// </summary>
    [HttpPost("test")]
    [ProducesResponseType(typeof(LlmTestResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LlmTestResultDto>> Test(CancellationToken cancellationToken)
    {
        var cfg = await _svc.GetAsync();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            string reply;

            if (cfg.LlmProvider == "Google" && !string.IsNullOrWhiteSpace(cfg.GoogleApiKey))
            {
                // Google Gemini path
                var model = string.IsNullOrWhiteSpace(cfg.GoogleGeminiModel) ? "gemini-1.5-pro" : cfg.GoogleGeminiModel;
                reply = await GeminiHttpHelper.CallAsync(
                    _httpFactory,
                    cfg.GoogleApiKey,
                    model,
                    systemPrompt: "You are a helpful assistant. Reply concisely.",
                    userPrompt: "hi",
                    temperature: 0.1f,
                    cancellationToken);
            }
            else if (!string.IsNullOrWhiteSpace(cfg.LlmEndpoint))
            {
                // Azure OpenAI / OpenAI path
                var (endpoint, deployment) = AzureOpenAIHelper.NormalizeEndpoint(cfg.LlmEndpoint, cfg.LlmDeployment);
                var chatClient = AzureOpenAIHelper.CreateClient(endpoint, cfg.LlmApiKey, deployment);
                var completion = await chatClient.CompleteChatAsync(
                    new List<ChatMessage>
                    {
                        new SystemChatMessage("You are a helpful assistant. Reply concisely."),
                        new UserChatMessage("hi")
                    },
                    new ChatCompletionOptions { MaxOutputTokenCount = 64 },
                    cancellationToken);
                reply = completion.Value.Content[0].Text;
            }
            else
            {
                sw.Stop();
                return Ok(new LlmTestResultDto
                {
                    Success = false,
                    Message = "No LLM is configured. Please set LlmEndpoint (Azure/OpenAI) or GoogleApiKey (Gemini) in AI Settings.",
                    LatencyMs = sw.ElapsedMilliseconds
                });
            }

            sw.Stop();
            return Ok(new LlmTestResultDto
            {
                Success = true,
                Message = reply,
                LatencyMs = sw.ElapsedMilliseconds
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Ok(new LlmTestResultDto
            {
                Success = false,
                Message = ex.Message,
                LatencyMs = sw.ElapsedMilliseconds
            });
        }
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
        SpeechProvider = cfg.SpeechProvider,
        SpeechEndpoint = cfg.SpeechEndpoint,
        SpeechApiKey = string.IsNullOrEmpty(cfg.SpeechApiKey) ? "" : "***",
        GoogleApiKey = string.IsNullOrEmpty(cfg.GoogleApiKey) ? "" : "***",
        GoogleGeminiModel = cfg.GoogleGeminiModel,
        UpdatedAt = cfg.UpdatedAt
    };
}
