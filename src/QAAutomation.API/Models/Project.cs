using System.ComponentModel.DataAnnotations;

namespace QAAutomation.API.Models;

/// <summary>
/// Top-level tenant. Everything (parameters, forms, audits, KB) is scoped under a Project.
/// Hierarchy: Project → LOB → EvaluationForm.
/// </summary>
public class Project
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    // ── PII / SPII Protection ────────────────────────────────────────────────
    /// <summary>
    /// When true, the system scans call transcripts for PII/SPII before sending
    /// them to the LLM.  Behaviour is controlled by <see cref="PiiRedactionMode"/>.
    /// </summary>
    public bool PiiProtectionEnabled { get; set; } = false;

    /// <summary>
    /// "Redact"  — replace detected PII tokens with labelled placeholders (e.g. [EMAIL])
    ///             before the transcript reaches the LLM.
    /// "Block"   — refuse to run the AI audit entirely when PII is detected; return an
    ///             error to the caller.
    /// Ignored when <see cref="PiiProtectionEnabled"/> is false.
    /// </summary>
    public string PiiRedactionMode { get; set; } = "Redact";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Lob> Lobs { get; set; } = new List<Lob>();
    public ICollection<UserProjectAccess> UserAccess { get; set; } = new List<UserProjectAccess>();
}
