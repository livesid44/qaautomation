namespace QAAutomation.API.Services;

/// <summary>
/// Detects when an AI-generated reasoning string contradicts a PASS score on a binary
/// compliance/quality field.  When the reasoning describes what the agent *failed* to do,
/// the field should be scored FAIL (0) rather than PASS (1), regardless of what the raw
/// LLM number returned.
///
/// The detector uses a curated set of negative-intent phrases that are unambiguously about
/// the agent not performing an action.  It deliberately avoids overly broad negations (e.g.
/// bare "not") to minimise false positives on double-negatives or incidental uses.
/// </summary>
internal static class NegativeIntentDetector
{
    /// <summary>
    /// Phrases that, when found in a reasoning string for a binary PASS field, indicate the
    /// LLM's own evidence contradicts the PASS score it assigned.
    ///
    /// Rules for adding phrases:
    ///  • The phrase must describe the *agent* failing to perform something.
    ///  • It must be unambiguous even without surrounding context.
    ///  • Double-negatives that are net-positive ("did not fail to") are handled by ordering
    ///    — the double-negative phrases are checked first and, if matched, return false.
    /// </summary>
    private static readonly string[] NegativePhrases =
    [
        // ── "did not / didn't" + action verb ────────────────────────────────
        "did not demonstrate",
        "didn't demonstrate",
        "did not show",
        "didn't show",
        "did not exhibit",
        "didn't exhibit",
        "did not display",
        "didn't display",
        "did not address",
        "didn't address",
        "did not acknowledge",
        "didn't acknowledge",
        "did not offer",
        "didn't offer",
        "did not provide",
        "didn't provide",
        "did not attempt",
        "didn't attempt",
        "did not follow",
        "didn't follow",
        "did not adhere",
        "didn't adhere",
        "did not meet",
        "didn't meet",
        "did not comply",
        "didn't comply",
        "did not express",
        "didn't express",
        "did not make",
        "didn't make",
        "did not use",
        "didn't use",
        "did not apply",
        "didn't apply",

        // ── "failed to / failure to" ─────────────────────────────────────────
        "failed to demonstrate",
        "failed to show",
        "failed to exhibit",
        "failed to address",
        "failed to acknowledge",
        "failed to provide",
        "failed to offer",
        "failed to attempt",
        "failed to follow",
        "failed to adhere",
        "failed to meet",
        "failed to comply",
        "failed to express",
        "failed to apply",

        // ── Absence / lack vocabulary ────────────────────────────────────────
        "lacked empathy",
        "lacked the",
        "lack of empathy",
        "lack of acknowledgment",
        "lack of acknowledgement",
        "lack of compassion",
        "lack of understanding",
        "showed no empathy",
        "showed no compassion",
        "no evidence of",
        "no demonstration of",
        "no attempt to",
        "absent from the",
        "was absent",
        "not observed",
        "not evident",
        "not present in",
        "not demonstrated",
        "not exhibited",
        "not shown",
        "not addressed",
        "not acknowledged",
        "not provided",

        // ── Neglect / omission vocabulary ────────────────────────────────────
        "neglected to",
        "omitted",
        "overlooked",
        "ignored the",
        "did not recognize",
        "didn't recognize",
        "failed to recognize",
    ];

    /// <summary>
    /// Double-negative or positive-framing overrides: if ANY of these are found, the
    /// reasoning is net-positive and we do NOT apply the negative-intent correction.
    /// Checked before <see cref="NegativePhrases"/>.
    /// </summary>
    private static readonly string[] PositiveOverridePhrases =
    [
        "did not fail",
        "didn't fail",
        "did not neglect",
        "didn't neglect",
        "did not miss",
        "didn't miss",
    ];

    /// <summary>
    /// Returns <c>true</c> when the reasoning text contains clear evidence that the agent
    /// failed to perform the evaluated behaviour, making a PASS score incorrect.
    /// </summary>
    /// <param name="reasoning">The LLM-generated reasoning string for the field.</param>
    public static bool HasNegativeIntent(string reasoning)
    {
        if (string.IsNullOrWhiteSpace(reasoning)) return false;

        var lower = reasoning.ToLowerInvariant();

        // If a positive override is present, treat the whole reasoning as net-positive
        foreach (var phrase in PositiveOverridePhrases)
            if (lower.Contains(phrase)) return false;

        foreach (var phrase in NegativePhrases)
            if (lower.Contains(phrase)) return true;

        return false;
    }
}
