using System.ComponentModel.DataAnnotations;

namespace QAAutomation.API.Models;

/// <summary>
/// A single text document (or chunked excerpt) stored in the knowledge base.
/// Used as context when evaluating KnowledgeBased-type form fields via RAG.
/// </summary>
public class KnowledgeDocument
{
    public int Id { get; set; }

    public int SourceId { get; set; }
    public KnowledgeSource Source { get; set; } = null!;

    [Required, MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Original filename or remote path.</summary>
    [MaxLength(1000)]
    public string? FileName { get; set; }

    /// <summary>Full text content of this document / chunk.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Comma-separated tags to restrict which QA parameters this doc applies to (optional).</summary>
    public string? Tags { get; set; }

    public long ContentSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
