using QAAutomation.API.DTOs;

namespace QAAutomation.API.Services;

/// <summary>
/// Runtime-selecting auto-audit service.
/// Routes to:
///   • <see cref="GoogleGeminiAutoAuditService"/>  when LlmProvider = "Google"
///   • <see cref="AzureOpenAIAutoAuditService"/>   when LlmEndpoint is set (Azure or OpenAI)
///   • <see cref="MockAutoAuditService"/>           when no endpoint is configured
/// </summary>
public class RuntimeAutoAuditService : IAutoAuditService
{
    private readonly IAiConfigService _aiConfig;
    private readonly AzureOpenAIAutoAuditService _azureService;
    private readonly GoogleGeminiAutoAuditService _googleService;
    private readonly MockAutoAuditService _mock;

    public RuntimeAutoAuditService(
        IAiConfigService aiConfig,
        AzureOpenAIAutoAuditService azureService,
        GoogleGeminiAutoAuditService googleService,
        MockAutoAuditService mock)
    {
        _aiConfig = aiConfig;
        _azureService = azureService;
        _googleService = googleService;
        _mock = mock;
    }

    public async Task<AutoAuditResponseDto> AnalyzeTranscriptAsync(
        AutoAuditRequestDto request,
        IEnumerable<AutoAuditFieldDefinition> fields,
        string formName,
        int? projectId = null,
        CancellationToken cancellationToken = default)
    {
        var cfg = await _aiConfig.GetAsync();

        if (cfg.LlmProvider == "Google" && !string.IsNullOrWhiteSpace(cfg.GoogleApiKey))
            return await _googleService.AnalyzeTranscriptAsync(request, fields, formName, projectId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(cfg.LlmEndpoint))
            return await _azureService.AnalyzeTranscriptAsync(request, fields, formName, projectId, cancellationToken);

        return await _mock.AnalyzeTranscriptAsync(request, fields, formName, projectId, cancellationToken);
    }
}

/// <summary>
/// Runtime-selecting sentiment service.
/// Routes to:
///   • <see cref="GoogleGeminiSentimentService"/> when SentimentProvider = "Google"
///   • <see cref="AzureOpenAISentimentService"/>  when LlmEndpoint is set (Azure or OpenAI)
///   • <see cref="MockSentimentService"/>          when no endpoint is configured
/// </summary>
public class RuntimeSentimentService : ISentimentService
{
    private readonly IAiConfigService _aiConfig;
    private readonly AzureOpenAISentimentService _azureService;
    private readonly GoogleGeminiSentimentService _googleService;
    private readonly MockSentimentService _mock;

    public RuntimeSentimentService(
        IAiConfigService aiConfig,
        AzureOpenAISentimentService azureService,
        GoogleGeminiSentimentService googleService,
        MockSentimentService mock)
    {
        _aiConfig = aiConfig;
        _azureService = azureService;
        _googleService = googleService;
        _mock = mock;
    }

    public async Task<SentimentAnalysisResponseDto> AnalyzeAsync(
        SentimentAnalysisRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var cfg = await _aiConfig.GetAsync();

        if (cfg.SentimentProvider == "Google" && !string.IsNullOrWhiteSpace(cfg.GoogleApiKey))
            return await _googleService.AnalyzeAsync(request, cancellationToken);

        if (!string.IsNullOrWhiteSpace(cfg.LlmEndpoint))
            return await _azureService.AnalyzeAsync(request, cancellationToken);

        return await _mock.AnalyzeAsync(request, cancellationToken);
    }
}

/// <summary>
/// Runtime-selecting speech-to-text service.
/// Routes to:
///   • <see cref="GoogleSpeechService"/> when SpeechProvider = "Google"
///   • <see cref="AzureSpeechService"/>  otherwise
/// </summary>
public class RuntimeSpeechService : IAzureSpeechService
{
    private readonly IAiConfigService _aiConfig;
    private readonly AzureSpeechService _azureService;
    private readonly GoogleSpeechService _googleService;

    public RuntimeSpeechService(
        IAiConfigService aiConfig,
        AzureSpeechService azureService,
        GoogleSpeechService googleService)
    {
        _aiConfig = aiConfig;
        _azureService = azureService;
        _googleService = googleService;
    }

    public async Task<string?> TranscribeAudioUrlAsync(string audioUrl, CancellationToken ct = default)
    {
        var cfg = await _aiConfig.GetAsync();

        if (cfg.SpeechProvider == "Google" && !string.IsNullOrWhiteSpace(cfg.GoogleApiKey))
            return await _googleService.TranscribeAudioUrlAsync(audioUrl, ct);

        return await _azureService.TranscribeAudioUrlAsync(audioUrl, ct);
    }
}
