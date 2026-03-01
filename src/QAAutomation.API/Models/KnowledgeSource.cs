using System.ComponentModel.DataAnnotations;

namespace QAAutomation.API.Models;

/// <summary>
/// Represents a configured knowledge-base source (connector).
/// Documents ingested from this source are stored in KnowledgeDocument.
/// </summary>
public class KnowledgeSource
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>"ManualUpload", "AzureBlob", "SFTP", "SharePoint"</summary>
    [Required, MaxLength(50)]
    public string ConnectorType { get; set; } = "ManualUpload";

    /// <summary>Free-form description of what this source covers.</summary>
    public string? Description { get; set; }

    // ── AzureBlob ─────────────────────────────────────────────────────────────
    public string? BlobConnectionString { get; set; }
    public string? BlobContainerName { get; set; }

    // ── SFTP ─────────────────────────────────────────────────────────────────
    public string? SftpHost { get; set; }
    public int? SftpPort { get; set; }
    public string? SftpUsername { get; set; }
    public string? SftpPassword { get; set; }
    public string? SftpPath { get; set; }

    // ── SharePoint ────────────────────────────────────────────────────────────
    public string? SharePointSiteUrl { get; set; }
    public string? SharePointClientId { get; set; }
    public string? SharePointClientSecret { get; set; }
    public string? SharePointLibraryName { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSyncedAt { get; set; }

    public ICollection<KnowledgeDocument> Documents { get; set; } = new List<KnowledgeDocument>();
}
