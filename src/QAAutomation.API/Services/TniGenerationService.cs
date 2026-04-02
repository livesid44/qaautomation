using System.Text;
using System.Text.Json;
using OpenAI.Chat;
using QAAutomation.API.DTOs;
using QAAutomation.API.Models;

namespace QAAutomation.API.Services;

/// <summary>
/// Generates LLM-powered training content and multiple-choice assessments for TNI plans.
///
/// Given the list of parameters that an agent scored below threshold in an evaluation,
/// the service produces:
///   1. A structured training document (plain text) covering the improvement areas.
///   2. A set of MCQ questions (with options, correct answer, and explanation) to assess
///      whether the agent has understood the training material.
///
/// Output is stored back on the <see cref="TrainingPlan"/> as
/// <c>LlmTrainingContent</c> and <c>AssessmentJson</c>.
/// </summary>
public class TniGenerationService
{
    private readonly IAiConfigService _aiConfig;
    private readonly ILogger<TniGenerationService> _logger;

    public TniGenerationService(IAiConfigService aiConfig, ILogger<TniGenerationService> logger)
    {
        _aiConfig = aiConfig;
        _logger = logger;
    }

    /// <summary>
    /// Calls the configured LLM to generate training content and MCQ questions for the
    /// specified <paramref name="failedAreas"/>. Returns the generated content and JSON,
    /// or throws when the LLM is not configured or an error occurs.
    /// </summary>
    /// <param name="failedAreas">The parameter labels / target areas that scored below threshold.</param>
    /// <param name="agentName">Agent name for personalised prompting.</param>
    /// <param name="formName">QA form name for context.</param>
    /// <param name="questionCount">Number of MCQ questions to generate (default 5).</param>
    public async Task<(string TrainingContent, string AssessmentJson)> GenerateAsync(
        IEnumerable<string> failedAreas,
        string agentName,
        string formName,
        int questionCount = 5,
        CancellationToken ct = default)
    {
        var cfg = await _aiConfig.GetAsync();
        if (string.IsNullOrWhiteSpace(cfg.LlmEndpoint) || string.IsNullOrWhiteSpace(cfg.LlmApiKey))
            throw new InvalidOperationException("LLM is not configured. Please configure AI Settings before generating TNI content.");

        var areaList = failedAreas.ToList();
        if (areaList.Count == 0)
            throw new ArgumentException("At least one failed area must be provided for content generation.");

        var prompt = BuildPrompt(areaList, agentName, formName, questionCount);

        var (ep, dep) = AzureOpenAIHelper.NormalizeEndpoint(cfg.LlmEndpoint, cfg.LlmDeployment);
        var client = AzureOpenAIHelper.CreateClient(ep, cfg.LlmApiKey, dep);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(
                "You are an expert call-centre quality assurance trainer. " +
                "You produce clear, practical training material and quiz questions in strict JSON format. " +
                "Always respond with valid JSON only — no markdown fences, no preamble."),
            new UserChatMessage(prompt)
        };

        var opts = new ChatCompletionOptions
        {
            Temperature = 0.4f,
            MaxOutputTokenCount = 3000,
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        ChatCompletion resp;
        try
        {
            resp = (await client.CompleteChatAsync(messages, opts, ct)).Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM call failed during TNI generation");
            throw new InvalidOperationException($"LLM call failed: {ex.Message}", ex);
        }

        var rawJson = resp.Content[0].Text.Trim();
        return ParseLlmOutput(rawJson, questionCount);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string BuildPrompt(List<string> areas, string agentName, string formName, int questionCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"An agent named \"{agentName}\" was evaluated against the QA form \"{formName}\".");
        sb.AppendLine("They scored below the acceptable threshold in the following parameter(s):");
        foreach (var area in areas)
            sb.AppendLine($"  - {area}");
        sb.AppendLine();
        sb.AppendLine($"Generate a training module with exactly {questionCount} multiple-choice questions.");
        sb.AppendLine();
        sb.AppendLine("Respond with a single JSON object in this exact schema (no markdown, no extra text):");
        sb.AppendLine("{");
        sb.AppendLine("  \"trainingContent\": \"<Full training text — minimum 300 words — covering each parameter with practical tips and examples>\",");
        sb.AppendLine("  \"questions\": [");
        sb.AppendLine("    {");
        sb.AppendLine("      \"question\": \"<question text>\",");
        sb.AppendLine("      \"options\": [\"<A>\", \"<B>\", \"<C>\", \"<D>\"],");
        sb.AppendLine("      \"correctIndex\": <0-3>,");
        sb.AppendLine("      \"explanation\": \"<brief explanation of why the correct answer is right>\"");
        sb.AppendLine("    }");
        sb.AppendLine($"  ]  // exactly {questionCount} items");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static (string TrainingContent, string AssessmentJson) ParseLlmOutput(string rawJson, int expectedCount)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(rawJson); }
        catch (JsonException) { throw new InvalidOperationException("LLM returned invalid JSON for TNI generation."); }

        var root = doc.RootElement;

        var trainingContent = root.TryGetProperty("trainingContent", out var tc)
            ? tc.GetString() ?? string.Empty
            : string.Empty;

        if (string.IsNullOrWhiteSpace(trainingContent))
            throw new InvalidOperationException("LLM did not return training content.");

        if (!root.TryGetProperty("questions", out var questionsEl) ||
            questionsEl.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("LLM did not return questions array.");

        var questions = new List<object>();
        int idx = 0;
        foreach (var q in questionsEl.EnumerateArray())
        {
            var options = new List<string>();
            if (q.TryGetProperty("options", out var optEl) && optEl.ValueKind == JsonValueKind.Array)
                foreach (var o in optEl.EnumerateArray())
                    options.Add(o.GetString() ?? "");

            questions.Add(new
            {
                question = q.TryGetProperty("question", out var qEl) ? qEl.GetString() ?? "" : "",
                options,
                correctIndex = q.TryGetProperty("correctIndex", out var ciEl) ? ciEl.GetInt32() : 0,
                explanation = q.TryGetProperty("explanation", out var exEl) ? exEl.GetString() ?? "" : ""
            });
            idx++;
            if (idx >= expectedCount) break;
        }

        if (questions.Count == 0)
            throw new InvalidOperationException("LLM returned zero questions for the assessment.");

        var assessmentJson = JsonSerializer.Serialize(questions);
        return (trainingContent, assessmentJson);
    }
}
