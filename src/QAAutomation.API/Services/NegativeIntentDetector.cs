namespace QAAutomation.API.Services;

/// <summary>
/// Detects when an AI-generated reasoning string contradicts a PASS score on a binary
/// compliance/quality field.  When the reasoning describes what the agent *failed* to do,
/// the field should be scored FAIL (0) rather than PASS (1), regardless of what the raw
/// LLM number returned.
///
/// Detection is intentionally conservative to prevent false positives:
///  • Ambiguous subjectless passive phrases ("not observed", "was absent") are excluded.
///  • A proximity-based subject check prevents flagging cases where the negative phrase
///    describes the *customer's* eligibility or situation rather than the agent's failure
///    (e.g. "the creator did not meet the subscriber threshold").
///  • "Not required" patterns confirm the absence of action was correct per policy scope.
/// </summary>
internal static class NegativeIntentDetector
{
    // ── Configuration ─────────────────────────────────────────────────────────

    /// <summary>
    /// Characters immediately before a matched phrase that are examined to identify its
    /// grammatical subject.  ~60 chars covers one short clause without picking up
    /// agent-words from a previous sentence.
    /// </summary>
    private const int SubjectProximityChars = 60;

    // ── Subject word lists ────────────────────────────────────────────────────

    /// <summary>
    /// Agent-role words.  When the closest subject before a negative phrase is one of
    /// these, the phrase describes an agent failure and should trigger the correction.
    /// </summary>
    private static readonly string[] AgentSubjectWords =
    [
        "agent", "representative", "advisor", "associate", "operator", "specialist"
    ];

    /// <summary>
    /// Customer / entity words.  When the closest subject before a negative phrase is one
    /// of these (with no nearer agent-role word), the negation describes the customer's
    /// situation or eligibility — not an agent failure — and the phrase is skipped.
    /// </summary>
    private static readonly string[] CustomerSubjectWords =
    [
        "customer", "creator", "caller", "user", "client", "subscriber", "channel"
    ];

    // ── Phrase lists ──────────────────────────────────────────────────────────

    /// <summary>
    /// Phrases that, when the agent is the grammatical subject, unambiguously describe
    /// the agent failing to perform the evaluated behaviour.
    ///
    /// Ambiguous subjectless passive phrases ("not observed", "not evident", "was absent")
    /// are intentionally absent; they fire too easily on policy/situation statements.
    /// Their active-voice counterparts below cover the same intent without the noise.
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

        // ── "failed to" ──────────────────────────────────────────────────────
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

        // ── Passive-voice absence (subject validated by proximity check) ─────
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
    /// Double-negative or "absence-was-correct" overrides: if ANY of these are found,
    /// the reasoning is net-positive and correction is NOT applied.
    /// Checked before <see cref="NegativePhrases"/>.
    /// </summary>
    private static readonly string[] PositiveOverridePhrases =
    [
        // Double-negatives (net-positive outcome)
        "did not fail",
        "didn't fail",
        "did not neglect",
        "didn't neglect",
        "did not miss",
        "didn't miss",

        // Absence of action was intentionally correct per policy or call scope
        "did not need to",
        "didn't need to",
        "was not required to",
        "not required to",
        "not required by",
        "does not require",
    ];

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> when the reasoning text contains clear evidence that the agent
    /// failed to perform the evaluated behaviour, making a PASS score incorrect.
    ///
    /// A proximity window check prevents false positives when the negative phrase refers
    /// to the customer's situation or eligibility rather than the agent's failure
    /// (e.g. "the creator did not meet the subscriber threshold").
    /// </summary>
    /// <param name="reasoning">The LLM-generated reasoning string for the field.</param>
    public static bool HasNegativeIntent(string reasoning)
    {
        if (string.IsNullOrWhiteSpace(reasoning)) return false;

        var lower = reasoning.ToLowerInvariant();

        // 1. Document-level positive overrides: double-negatives and "not required" patterns.
        //    If any of these appear anywhere in the reasoning, treat it as net-positive.
        foreach (var phrase in PositiveOverridePhrases)
            if (lower.Contains(phrase)) return false;

        // 2. Per-phrase check with subject-proximity validation.
        //    For each phrase hit, examine the text immediately preceding it to identify the
        //    closest grammatical subject.  Last-occurrence ordering is used so the word
        //    nearest the phrase (most likely its direct subject) takes precedence.
        //    If that subject is a customer/entity word rather than an agent-role word, the
        //    negation describes the customer's situation — not an agent failure — and is skipped.
        foreach (var phrase in NegativePhrases)
        {
            int idx = lower.IndexOf(phrase, StringComparison.Ordinal);
            if (idx < 0) continue;

            int windowStart = Math.Max(0, idx - SubjectProximityChars);
            var window = lower.Substring(windowStart, idx - windowStart);

            int lastAgentIdx    = AgentSubjectWords   .Max(w => window.LastIndexOf(w, StringComparison.Ordinal));
            int lastCustomerIdx = CustomerSubjectWords .Max(w => window.LastIndexOf(w, StringComparison.Ordinal));

            // If the closest subject is a customer/entity word, the negation describes
            // the customer's situation — not an agent failure — so skip this phrase.
            // When neither subject type appears in the window (both -1), the phrase has
            // no explicit subject and is kept as a potential agent failure — the
            // conservative choice for subjectless constructions such as
            // "No evidence of authentication steps being followed."
            if (lastCustomerIdx > lastAgentIdx)
                continue;

            return true;
        }

        return false;
    }
}
