using System.Diagnostics;
using System.Text;
using System.Text.Json;
using QAAutomation.API.DTOs;

namespace QAAutomation.API.Services;

/// <summary>
/// Analyzes call transcripts using the Google Gemini API to score QA evaluation form fields.
/// Uses the Google AI Studio REST endpoint (requires only an API key — no Google Cloud project needed).
///
/// Endpoint: POST https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent
/// Reference: https://ai.google.dev/api/generate-content
/// </summary>
public class GoogleGeminiAutoAuditService : IAutoAuditService
{
    private readonly IAiConfigService _aiConfig;
    private readonly IKnowledgeBaseService _kb;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<GoogleGeminiAutoAuditService> _logger;

    public GoogleGeminiAutoAuditService(
        IAiConfigService aiConfig,
        IKnowledgeBaseService kb,
        IHttpClientFactory httpFactory,
        IAuditLogService auditLog,
        ILogger<GoogleGeminiAutoAuditService> logger)
    {
        _aiConfig = aiConfig;
        _kb = kb;
        _httpFactory = httpFactory;
        _auditLog = auditLog;
        _logger = logger;
    }

    public async Task<AutoAuditResponseDto> AnalyzeTranscriptAsync(
        AutoAuditRequestDto request,
        IEnumerable<AutoAuditFieldDefinition> fields,
        string formName,
        int? projectId = null,
        CancellationToken cancellationToken = default)
    {
        var fieldList = fields.ToList();
        var cfg = await _aiConfig.GetAsync();

        var response = new AutoAuditResponseDto
        {
            FormId = request.FormId,
            FormName = formName,
            Transcript = request.Transcript,
            EvaluatedBy = request.EvaluatedBy,
            AgentName = request.AgentName,
            CallReference = request.CallReference,
            CallDate = request.CallDate,
            IsAiGenerated = true
        };

        var endpoint = $"{GeminiHttpHelper.BaseUrl}{cfg.GoogleGeminiModel}:generateContent";
        var sw = Stopwatch.StartNew();
        try
        {
            // Retrieve KB context concurrently for KnowledgeBased fields
            var kbContextMap = new Dictionary<int, string>();
            var kbFields = fieldList.Where(f => f.EvaluationType == "KnowledgeBased").ToList();
            if (kbFields.Count > 0)
            {
                var tasks = kbFields.Select(f =>
                    _kb.RetrieveAsync($"{f.Label} {f.Description}", cfg.RagTopK, null, projectId)
                       .ContinueWith(t => (f.FieldId, chunks: t.Result)));
                var results = await Task.WhenAll(tasks);
                foreach (var (fieldId, chunks) in results)
                    if (chunks.Count > 0)
                        kbContextMap[fieldId] = string.Join("\n\n", chunks);
            }

            var systemPrompt = AzureOpenAIAutoAuditService.BuildSystemPrompt(formName, fieldList, kbContextMap);
            var userPrompt = BuildUserPrompt(request.Transcript, fieldList);

            var jsonText = await GeminiHttpHelper.CallAsync(
                _httpFactory, cfg.GoogleApiKey, cfg.GoogleGeminiModel,
                systemPrompt, userPrompt, cfg.LlmTemperature, cancellationToken);

            ParseGeminiResponse(jsonText, fieldList, response);
            sw.Stop();

            await _auditLog.LogExternalApiCallAsync(
                projectId, "LlmAudit", "Success", "GoogleGemini",
                endpoint, "POST", 200, sw.ElapsedMilliseconds, request.EvaluatedBy,
                $"Form: {formName}; Model: {cfg.GoogleGeminiModel}; CallRef: {request.CallReference}",
                cancellationToken);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Google Gemini auto-audit analysis failed for form {FormId}", request.FormId);
            response.AnalysisError = $"Google Gemini analysis failed: {ex.Message}";
            response.IsAiGenerated = false;
            FillNeutralScores(fieldList, response);

            await _auditLog.LogExternalApiCallAsync(
                projectId, "LlmAudit", "Failure", "GoogleGemini",
                endpoint, "POST", null, sw.ElapsedMilliseconds, request.EvaluatedBy,
                $"Form: {formName}; Model: {cfg.GoogleGeminiModel}; Error: {ex.Message[..Math.Min(200, ex.Message.Length)]}",
                cancellationToken);
        }

        return response;
    }

    private static string BuildUserPrompt(string transcript, List<AutoAuditFieldDefinition> fields)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Please evaluate the following call transcript:");
        sb.AppendLine();
        sb.AppendLine("--- TRANSCRIPT START ---");
        sb.AppendLine(transcript);
        sb.AppendLine("--- TRANSCRIPT END ---");
        sb.AppendLine();
        sb.AppendLine($"Score all {fields.Count} fields listed in the system prompt. Return valid JSON only.");
        return sb.ToString();
    }

    private static void ParseGeminiResponse(string json, List<AutoAuditFieldDefinition> fields, AutoAuditResponseDto response)
    {
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("suggestedAgentName", out var agentEl) &&
            agentEl.ValueKind == JsonValueKind.String &&
            string.IsNullOrWhiteSpace(response.AgentName))
            response.AgentName = agentEl.GetString();

        if (root.TryGetProperty("suggestedCallReference", out var refEl) &&
            refEl.ValueKind == JsonValueKind.String &&
            string.IsNullOrWhiteSpace(response.CallReference))
            response.CallReference = refEl.GetString();

        if (root.TryGetProperty("overallReasoning", out var orEl))
            response.OverallReasoning = orEl.GetString() ?? "";

        if (root.TryGetProperty("scores", out var scoresEl) && scoresEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var scoreEl in scoresEl.EnumerateArray())
            {
                if (!scoreEl.TryGetProperty("fieldId", out var fidEl)) continue;
                var fieldId = fidEl.GetInt32();
                var field = fields.FirstOrDefault(f => f.FieldId == fieldId);
                if (field == null) continue;

                double score = 0;
                if (scoreEl.TryGetProperty("score", out var scoreValEl))
                    score = scoreValEl.ValueKind == JsonValueKind.Number ? scoreValEl.GetDouble() : 0;

                if (field.MaxRating == 1)
                    score = score >= 0.5 ? 1 : 0;
                else
                    score = Math.Max(0, Math.Min(field.MaxRating, score));

                var reasoning = scoreEl.TryGetProperty("reasoning", out var rEl) ? rEl.GetString() ?? "" : "";

                var suggestedScore = score;
                if (field.MaxRating == 1 && score == 1 && NegativeIntentDetector.HasNegativeIntent(reasoning))
                {
                    score = 0;
                    reasoning += " [Score corrected PASS→FAIL: reasoning indicates agent did not perform this behaviour.]";
                }

                response.Fields.Add(new AutoAuditFieldScoreDto
                {
                    FieldId = fieldId,
                    FieldLabel = field.Label,
                    SectionTitle = field.SectionTitle,
                    MaxRating = field.MaxRating,
                    SuggestedScore = suggestedScore,
                    FinalScore = score,
                    Reasoning = reasoning
                });
            }
        }

        foreach (var field in fields.Where(f => response.Fields.All(s => s.FieldId != f.FieldId)))
        {
            double defaultScore = field.MaxRating == 1 ? 1 : 3;
            response.Fields.Add(new AutoAuditFieldScoreDto
            {
                FieldId = field.FieldId,
                FieldLabel = field.Label,
                SectionTitle = field.SectionTitle,
                MaxRating = field.MaxRating,
                SuggestedScore = defaultScore,
                FinalScore = defaultScore,
                Reasoning = "Insufficient evidence in transcript — defaulted to standard score."
            });
        }
    }

    private static void FillNeutralScores(List<AutoAuditFieldDefinition> fields, AutoAuditResponseDto response)
    {
        response.Fields = fields.Select(f =>
        {
            double defaultScore = f.MaxRating == 1 ? 1 : 3;
            return new AutoAuditFieldScoreDto
            {
                FieldId = f.FieldId,
                FieldLabel = f.Label,
                SectionTitle = f.SectionTitle,
                MaxRating = f.MaxRating,
                SuggestedScore = defaultScore,
                FinalScore = defaultScore,
                Reasoning = "Score could not be determined automatically — please review manually."
            };
        }).ToList();
        response.OverallReasoning = "Automated analysis was not available. Scores are set to defaults — please review and adjust manually.";
    }
}
