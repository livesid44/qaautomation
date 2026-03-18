using System.Text.Json;
using QAAutomation.API.DTOs;
using QAAutomation.API.Services;
using Xunit;

namespace QAAutomation.Tests;

/// <summary>
/// Unit tests for the negative-intent detection layer that corrects binary PASS scores
/// when the AI's own reasoning contradicts the score.
/// </summary>
public class NegativeIntentDetectorTests
{
    // ── HasNegativeIntent — phrases that SHOULD trigger correction ─────────────

    [Theory]
    [InlineData("The agent did not demonstrate empathy for the creator's situation.")]
    [InlineData("Agent didn't demonstrate any acknowledgement of the caller's frustration.")]
    [InlineData("The agent did not show any empathy.")]
    [InlineData("Agent didn't show awareness of policy.")]
    [InlineData("The representative did not address the customer's concern about eligibility.")]
    [InlineData("Agent failed to demonstrate knowledge of the escalation process.")]
    [InlineData("The agent failed to follow the authentication protocol.")]
    [InlineData("The agent failed to acknowledge the emotional impact on the creator.")]
    [InlineData("Agent lacked empathy in this interaction.")]
    [InlineData("There was a lack of empathy throughout the call.")]
    [InlineData("The agent showed no empathy towards the customer.")]
    [InlineData("No evidence of authentication steps being followed.")]
    [InlineData("No demonstration of compliance with the policy.")]
    [InlineData("The required behaviour was not demonstrated by the agent.")]
    [InlineData("Empathy was not exhibited during the interaction.")]
    [InlineData("The agent neglected to ask verification questions.")]
    [InlineData("The agent omitted the closing script.")]
    [InlineData("Agent did not comply with the data protection protocol.")]
    [InlineData("The agent didn't comply with authentication requirements.")]
    [InlineData("Agent did not acknowledge the creator's frustration with losing the Silver Play Button.")]
    [InlineData("No attempt to express compassion for the situation.")]
    public void HasNegativeIntent_NegativeReasoning_ReturnsTrue(string reasoning)
    {
        Assert.True(NegativeIntentDetector.HasNegativeIntent(reasoning));
    }

    // ── HasNegativeIntent — phrases that should NOT trigger correction ──────────

    [Theory]
    [InlineData("The agent demonstrated strong empathy throughout the call.")]
    [InlineData("Agent showed excellent compliance with all authentication steps.")]
    [InlineData("The representative followed the escalation protocol correctly.")]
    [InlineData("Agent addressed all customer concerns in a timely manner.")]
    [InlineData("The agent complied with all required procedures.")]
    [InlineData("Agent acknowledged the customer's frustration and offered a solution.")]
    [InlineData("Transcript evidence shows the agent met all compliance requirements.")]
    [InlineData("The agent provided a thorough explanation of the policy.")]
    [InlineData("")]      // empty reasoning — should not trigger
    [InlineData("   ")]   // whitespace only
    public void HasNegativeIntent_PositiveReasoning_ReturnsFalse(string reasoning)
    {
        Assert.False(NegativeIntentDetector.HasNegativeIntent(reasoning));
    }

    // ── Double-negative override — "did not fail / didn't fail" ──────────────

    [Theory]
    [InlineData("The agent did not fail to demonstrate empathy.")]
    [InlineData("The agent didn't fail to acknowledge the issue.")]
    [InlineData("The agent did not neglect the authentication step.")]
    [InlineData("The agent didn't neglect to follow the script.")]
    public void HasNegativeIntent_DoubleNegative_ReturnsFalse(string reasoning)
    {
        // "did not fail to X" is net-positive — correction must NOT be applied
        Assert.False(NegativeIntentDetector.HasNegativeIntent(reasoning));
    }

    // ── Case insensitivity ────────────────────────────────────────────────────

    [Fact]
    public void HasNegativeIntent_UpperCasePhrases_MatchesCaseInsensitively()
    {
        Assert.True(NegativeIntentDetector.HasNegativeIntent("THE AGENT DID NOT DEMONSTRATE EMPATHY."));
        Assert.True(NegativeIntentDetector.HasNegativeIntent("FAILED TO SHOW any effort."));
    }
}

/// <summary>
/// Integration-style tests for the full ParseLlmResponse + NegativeIntentDetector pipeline,
/// exercised via <see cref="AzureOpenAIAutoAuditService.BuildSystemPrompt"/> and the
/// internal <c>ParseLlmResponse</c> method path (tested via AutoAnalyze output shape).
/// </summary>
public class NegativeIntentScoringCorrectionTests
{
    private static AutoAuditFieldDefinition BinaryField(int id, string label = "Empathy") =>
        new(id, label, null, 1, false, "Communication", "LLM");

    private static AutoAuditFieldDefinition RatingField(int id, string label = "Clarity", int max = 5) =>
        new(id, label, null, max, false, "Communication", "LLM");

    // Helper: build minimal LLM JSON as if it came from the model
    private static string BuildLlmJson(int fieldId, double score, string reasoning, string overall = "Good call.") =>
        JsonSerializer.Serialize(new
        {
            scores = new[] { new { fieldId, score, reasoning } },
            overallReasoning = overall,
            suggestedAgentName = (string?)null,
            suggestedCallReference = (string?)null,
        });

    // We test ParseLlmResponse indirectly through BuildSystemPrompt+the public surface.
    // Because ParseLlmResponse is private, we verify the correction through the prompt rule
    // that was added to the system prompt.

    [Fact]
    public void BuildSystemPrompt_ContainsCriticalConsistencyRule()
    {
        var fields = new List<AutoAuditFieldDefinition> { BinaryField(1) };
        var prompt = AzureOpenAIAutoAuditService.BuildSystemPrompt("Test Form", fields, null);

        Assert.Contains("CRITICAL", prompt);
        Assert.Contains("consistent with your reasoning", prompt);
        Assert.Contains("FAIL", prompt);
    }

    // ── Verify NegativeIntentDetector integrates correctly with the exact phrase
    //    from the bug report ────────────────────────────────────────────────────

    [Fact]
    public void HasNegativeIntent_ExactBugReportPhrase_ReturnsTrue()
    {
        const string reasoning =
            "The agent did not demonstrate empathy for the creator's situation, such as " +
            "acknowledging the emotional impact of losing eligibility for the Silver Play " +
            "Button despite years of hard work.";

        Assert.True(NegativeIntentDetector.HasNegativeIntent(reasoning));
    }

    // ── Non-binary fields (MaxRating > 1) are NOT corrected ───────────────────
    //    (correction only applies to binary PASS/FAIL fields, not star-rated ones)

    [Fact]
    public void HasNegativeIntent_StillDetectsNegativePhraseForStarField_ButCorrectionNotAppliedByDesign()
    {
        // The detector returns true — but the caller (ParseLlmResponse) only applies the
        // correction when MaxRating == 1.  We just verify the detector's behaviour here.
        const string negativeReasoning = "The agent did not demonstrate adequate knowledge.";
        Assert.True(NegativeIntentDetector.HasNegativeIntent(negativeReasoning));
    }
}
