using QAAutomation.API.DTOs;

namespace QAAutomation.API.Services;

/// <summary>
/// Mock auto-audit service used when Azure OpenAI is not configured.
/// Returns realistic-looking scores with placeholder reasoning to demonstrate the UX flow.
/// </summary>
public class MockAutoAuditService : IAutoAuditService
{
    private readonly ILogger<MockAutoAuditService> _logger;

    public MockAutoAuditService(ILogger<MockAutoAuditService> logger)
    {
        _logger = logger;
    }

    public Task<AutoAuditResponseDto> AnalyzeTranscriptAsync(
        AutoAuditRequestDto request,
        IEnumerable<AutoAuditFieldDefinition> fields,
        string formName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MockAutoAuditService: Azure OpenAI not configured — returning simulated scores");

        var fieldList = fields.ToList();
        var rng = new Random(request.Transcript.Length + request.FormId);

        // Build plausible mock scores per field with section-appropriate reasoning
        var scores = fieldList.Select(field =>
        {
            double score;
            string reasoning;

            if (field.MaxRating == 1)
            {
                // Compliance fields — mostly pass, occasionally flag one
                score = rng.NextDouble() > 0.15 ? 1 : 0;
                reasoning = score == 1
                    ? $"Agent demonstrated compliance with {field.Label} requirements throughout the interaction."
                    : $"[SIMULATED] Potential compliance concern detected for {field.Label} — requires manual review.";
            }
            else
            {
                // Quality fields — skew slightly above average (3-5 range)
                score = rng.Next(3, 6);
                reasoning = (int)score switch
                {
                    5 => $"Agent demonstrated exceptional {field.Label.ToLower()} — went above and beyond expectations in this interaction.",
                    4 => $"Agent showed strong {field.Label.ToLower()} with clear evidence in the transcript.",
                    3 => $"Agent met the standard for {field.Label.ToLower()} — no major issues observed.",
                    2 => $"Agent's {field.Label.ToLower()} was below standard — specific coaching recommended.",
                    _ => $"Significant issues with {field.Label.ToLower()} — immediate coaching required."
                };
                reasoning = "[SIMULATED] " + reasoning;
            }

            return new AutoAuditFieldScoreDto
            {
                FieldId = field.FieldId,
                FieldLabel = field.Label,
                SectionTitle = field.SectionTitle,
                MaxRating = field.MaxRating,
                SuggestedScore = score,
                FinalScore = score,
                Reasoning = reasoning
            };
        }).ToList();

        var total = scores.Sum(s => s.SuggestedScore);
        var max = scores.Sum(s => s.MaxRating);
        var pct = max > 0 ? total / max * 100 : 0;
        var grade = pct >= 90 ? "Outstanding" : pct >= 80 ? "strong" : pct >= 70 ? "acceptable" : "below standard";

        var response = new AutoAuditResponseDto
        {
            FormId = request.FormId,
            FormName = formName,
            Transcript = request.Transcript,
            EvaluatedBy = request.EvaluatedBy,
            AgentName = request.AgentName,
            CallReference = request.CallReference,
            CallDate = request.CallDate,
            Fields = scores,
            OverallReasoning = $"[SIMULATED — Azure OpenAI not configured] This call demonstrates {grade} overall performance with a score of {total:F0}/{max}. " +
                               "Connect Azure OpenAI to receive real AI-powered analysis of transcript content.",
            IsAiGenerated = false
        };

        return Task.FromResult(response);
    }
}
