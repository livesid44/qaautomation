using System.Text;
using System.Text.Json;
using OpenAI.Chat;
using QAAutomation.API.DTOs;
using QAAutomation.API.Models;

namespace QAAutomation.API.Services;

/// <summary>
/// Analyzes call transcripts using Azure OpenAI (GPT) to score QA evaluation form fields.
/// Configuration is read from the database (AiConfig) rather than appsettings.
/// KnowledgeBased fields are augmented with relevant KB context via RAG before scoring.
/// </summary>
public class AzureOpenAIAutoAuditService : IAutoAuditService
{
    private readonly IAiConfigService _aiConfig;
    private readonly IKnowledgeBaseService _kb;
    private readonly ILogger<AzureOpenAIAutoAuditService> _logger;

    public AzureOpenAIAutoAuditService(
        IAiConfigService aiConfig,
        IKnowledgeBaseService kb,
        ILogger<AzureOpenAIAutoAuditService> logger)
    {
        _aiConfig = aiConfig;
        _kb = kb;
        _logger = logger;
    }

    public async Task<AutoAuditResponseDto> AnalyzeTranscriptAsync(
        AutoAuditRequestDto request,
        IEnumerable<AutoAuditFieldDefinition> fields,
        string formName,
        CancellationToken cancellationToken = default)
    {
        var fieldList = fields.ToList();
        var cfg = await _aiConfig.GetAsync();
        var apiKey = cfg.LlmApiKey;
        var (endpoint, deployment) = AzureOpenAIHelper.NormalizeEndpoint(cfg.LlmEndpoint, cfg.LlmDeployment);

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

        try
        {
            var chatClient = AzureOpenAIHelper.CreateClient(endpoint, apiKey, deployment);

            // Retrieve KB context concurrently for all KnowledgeBased fields
            var kbContextMap = new Dictionary<int, string>();
            var kbFields = fieldList.Where(f => f.EvaluationType == "KnowledgeBased").ToList();
            if (kbFields.Count > 0)
            {
                var tasks = kbFields.Select(f =>
                    _kb.RetrieveAsync($"{f.Label} {f.Description}", cfg.RagTopK)
                       .ContinueWith(t => (f.FieldId, chunks: t.Result)));
                var results = await Task.WhenAll(tasks);
                foreach (var (fieldId, chunks) in results)
                    if (chunks.Count > 0)
                        kbContextMap[fieldId] = string.Join("\n\n", chunks);
            }

            var systemPrompt = BuildSystemPrompt(formName, fieldList, kbContextMap);
            var userPrompt = BuildUserPrompt(request.Transcript, fieldList);

            var options = new ChatCompletionOptions
            {
                Temperature = cfg.LlmTemperature,
                MaxOutputTokenCount = 4096,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            var completion = await chatClient.CompleteChatAsync(
                new List<ChatMessage> { new SystemChatMessage(systemPrompt), new UserChatMessage(userPrompt) },
                options, cancellationToken);

            ParseLlmResponse(completion.Value.Content[0].Text, fieldList, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure OpenAI auto-audit analysis failed for form {FormId}", request.FormId);
            response.AnalysisError = $"Azure OpenAI analysis failed: {ex.Message}";
            response.IsAiGenerated = false;
            FillNeutralScores(fieldList, response);
        }

        return response;
    }

    private static string BuildSystemPrompt(string formName, List<AutoAuditFieldDefinition> fields,
        Dictionary<int, string>? kbContextMap = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are an expert quality assurance evaluator for customer support calls.");
        sb.AppendLine($"Your task is to evaluate a call transcript against the '{formName}' QA evaluation form.");
        sb.AppendLine();
        sb.AppendLine("SCORING RULES:");
        sb.AppendLine("- For fields with MaxRating=5: score 1 (Unacceptable), 2 (Needs Improvement), 3 (Meets Standard), 4 (Exceeds Standard), 5 (Outstanding)");
        sb.AppendLine("- For fields with MaxRating=1: score 0 (FAIL) or 1 (PASS) — these are binary compliance checks");
        sb.AppendLine("- Be evidence-based: cite specific moments in the transcript for your reasoning");
        sb.AppendLine("- Fields marked [KB] must be scored against the provided Knowledge Base excerpts as the ground truth");
        sb.AppendLine("- If evidence is insufficient to score a field, default to 3 (Meets Standard) for 1-5 fields or 1 (PASS) for compliance");
        sb.AppendLine();
        sb.AppendLine("FORM FIELDS TO SCORE:");
        foreach (var g in fields.GroupBy(f => f.SectionTitle))
        {
            sb.AppendLine($"[{g.Key}]");
            foreach (var f in g)
            {
                var kbTag = f.EvaluationType == "KnowledgeBased" ? " [KB]" : "";
                sb.AppendLine($"  - FieldId={f.FieldId}, Label=\"{f.Label}\"{kbTag}, MaxRating={f.MaxRating}{(string.IsNullOrEmpty(f.Description) ? "" : $", Description=\"{f.Description}\"")}");
            }
        }

        // Inject KB context per field
        if (kbContextMap != null && kbContextMap.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("KNOWLEDGE BASE CONTEXT (use for [KB] fields):");
            foreach (var (fieldId, ctx) in kbContextMap)
            {
                var field = fields.FirstOrDefault(f => f.FieldId == fieldId);
                if (field == null) continue;
                sb.AppendLine($"--- KB for \"{field.Label}\" ---");
                sb.AppendLine(ctx);
                sb.AppendLine("---");
            }
        }

        sb.AppendLine();
        sb.AppendLine("RESPONSE FORMAT (JSON only, no other text):");
        sb.AppendLine("{");
        sb.AppendLine("  \"scores\": [");
        sb.AppendLine("    { \"fieldId\": <int>, \"score\": <number>, \"reasoning\": \"<specific evidence from transcript>\" }");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"overallReasoning\": \"<1-3 sentence overall call quality summary>\",");
        sb.AppendLine("  \"suggestedAgentName\": \"<agent name if mentioned, or null>\",");
        sb.AppendLine("  \"suggestedCallReference\": \"<call reference if mentioned, or null>\"");
        sb.AppendLine("}");
        return sb.ToString();
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

    private static void ParseLlmResponse(string json, List<AutoAuditFieldDefinition> fields, AutoAuditResponseDto response)
    {
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Only use the LLM-extracted agent name when the user did not provide one
        if (root.TryGetProperty("suggestedAgentName", out var agentEl) &&
            agentEl.ValueKind == JsonValueKind.String &&
            string.IsNullOrWhiteSpace(response.AgentName))
        {
            response.AgentName = agentEl.GetString();
        }

        if (root.TryGetProperty("suggestedCallReference", out var refEl) &&
            refEl.ValueKind == JsonValueKind.String &&
            string.IsNullOrWhiteSpace(response.CallReference))
        {
            response.CallReference = refEl.GetString();
        }

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

                // Clamp score to valid range
                score = Math.Max(0, Math.Min(field.MaxRating, score));

                var reasoning = scoreEl.TryGetProperty("reasoning", out var rEl) ? rEl.GetString() ?? "" : "";

                response.Fields.Add(new AutoAuditFieldScoreDto
                {
                    FieldId = fieldId,
                    FieldLabel = field.Label,
                    SectionTitle = field.SectionTitle,
                    MaxRating = field.MaxRating,
                    SuggestedScore = score,
                    FinalScore = score,
                    Reasoning = reasoning
                });
            }
        }

        // Fill in any fields that LLM missed
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
