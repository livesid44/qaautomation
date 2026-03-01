using QAAutomation.API.DTOs;

namespace QAAutomation.API.Services;

/// <summary>
/// Runtime-selecting auto-audit service: uses AzureOpenAI when LlmEndpoint is configured in the DB,
/// otherwise falls back to the mock service.
/// </summary>
public class RuntimeAutoAuditService : IAutoAuditService
{
    private readonly IAiConfigService _aiConfig;
    private readonly AzureOpenAIAutoAuditService _real;
    private readonly MockAutoAuditService _mock;

    public RuntimeAutoAuditService(
        IAiConfigService aiConfig,
        AzureOpenAIAutoAuditService real,
        MockAutoAuditService mock)
    {
        _aiConfig = aiConfig;
        _real = real;
        _mock = mock;
    }

    public async Task<AutoAuditResponseDto> AnalyzeTranscriptAsync(
        AutoAuditRequestDto request,
        IEnumerable<AutoAuditFieldDefinition> fields,
        string formName,
        CancellationToken cancellationToken = default)
    {
        var cfg = await _aiConfig.GetAsync();
        return string.IsNullOrWhiteSpace(cfg.LlmEndpoint)
            ? await _mock.AnalyzeTranscriptAsync(request, fields, formName, cancellationToken)
            : await _real.AnalyzeTranscriptAsync(request, fields, formName, cancellationToken);
    }
}

/// <summary>
/// Runtime-selecting sentiment service: uses AzureOpenAI when LlmEndpoint is configured in the DB,
/// otherwise falls back to the mock service.
/// </summary>
public class RuntimeSentimentService : ISentimentService
{
    private readonly IAiConfigService _aiConfig;
    private readonly AzureOpenAISentimentService _real;
    private readonly MockSentimentService _mock;

    public RuntimeSentimentService(
        IAiConfigService aiConfig,
        AzureOpenAISentimentService real,
        MockSentimentService mock)
    {
        _aiConfig = aiConfig;
        _real = real;
        _mock = mock;
    }

    public async Task<SentimentAnalysisResponseDto> AnalyzeAsync(
        SentimentAnalysisRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var cfg = await _aiConfig.GetAsync();
        return string.IsNullOrWhiteSpace(cfg.LlmEndpoint)
            ? await _mock.AnalyzeAsync(request, cancellationToken)
            : await _real.AnalyzeAsync(request, cancellationToken);
    }
}
