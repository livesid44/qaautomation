using System.Text.RegularExpressions;

namespace QAAutomation.API.Services;

/// <summary>
/// Detects and optionally redacts PII/SPII from free-text (e.g. call transcripts)
/// before the content is forwarded to any external LLM service.
///
/// Supported entity types:
///   EMAIL, PHONE, SSN, CREDIT_CARD, IP_ADDRESS, PASSPORT, DATE_OF_BIRTH, POSTAL_CODE
///
/// Redaction replaces each detected token with a placeholder such as [EMAIL],
/// preserving the structure of the text so QA scoring logic is unaffected.
/// </summary>
public static class PiiRedactionService
{
    // ── Pattern definitions ───────────────────────────────────────────────────

    private static readonly (string Label, Regex Pattern)[] _patterns =
    [
        // Email — RFC-5321-ish
        ("EMAIL",
            new Regex(@"\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // US phone — various formats: (xxx) xxx-xxxx, xxx-xxx-xxxx, +1xxxxxxxxxx, etc.
        ("PHONE",
            new Regex(@"(\+?1[\s\-.]?)?\(?\d{3}\)?[\s\-.]?\d{3}[\s\-.]?\d{4}\b",
                RegexOptions.Compiled)),

        // US SSN — xxx-xx-xxxx (hyphens or spaces)
        ("SSN",
            new Regex(@"\b\d{3}[\s\-]\d{2}[\s\-]\d{4}\b",
                RegexOptions.Compiled)),

        // Credit / debit card numbers (13–16 digit sequences, common separators)
        ("CREDIT_CARD",
            new Regex(@"\b(?:\d[ \-]?){13,16}\b",
                RegexOptions.Compiled)),

        // IPv4 address
        ("IP_ADDRESS",
            new Regex(@"\b(?:(?:25[0-5]|2[0-4]\d|[01]?\d\d?)\.){3}(?:25[0-5]|2[0-4]\d|[01]?\d\d?)\b",
                RegexOptions.Compiled)),

        // Passport-style identifiers: one or two letters followed by 6–9 digits
        ("PASSPORT",
            new Regex(@"\b[A-Z]{1,2}\d{6,9}\b",
                RegexOptions.Compiled)),

        // US ZIP / UK postcode (basic forms)
        ("POSTAL_CODE",
            new Regex(@"\b\d{5}(?:-\d{4})?\b|\b[A-Z]{1,2}\d[A-Z\d]?\s*\d[A-Z]{2}\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // Dates that look like DOB patterns: MM/DD/YYYY, DD-MM-YYYY, YYYY-MM-DD
        ("DATE_OF_BIRTH",
            new Regex(@"\b(?:\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4}|\d{4}[\/\-]\d{1,2}[\/\-]\d{1,2})\b",
                RegexOptions.Compiled)),
    ];

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the text contains at least one PII/SPII token.
    /// </summary>
    public static bool ContainsPii(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        foreach (var (_, pattern) in _patterns)
            if (pattern.IsMatch(text)) return true;
        return false;
    }

    /// <summary>
    /// Returns the types of PII detected (e.g. "EMAIL", "PHONE").
    /// Empty when no PII is found.
    /// </summary>
    public static IReadOnlyList<string> DetectTypes(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
        var found = new List<string>();
        foreach (var (label, pattern) in _patterns)
            if (pattern.IsMatch(text)) found.Add(label);
        return found;
    }

    /// <summary>
    /// Replaces every detected PII token with a labelled placeholder.
    /// The original text is returned unchanged when no PII is found.
    /// </summary>
    /// <param name="text">Input text (e.g. call transcript).</param>
    /// <returns>Redacted text.</returns>
    public static string Redact(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        // Apply patterns in declared order — EMAIL first so it isn't partially matched
        // by the PHONE pattern (phone patterns are digit-only and won't overlap email).
        foreach (var (label, pattern) in _patterns)
            text = pattern.Replace(text, $"[{label}]");

        return text;
    }
}
