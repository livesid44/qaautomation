using Microsoft.EntityFrameworkCore;
using QAAutomation.API.Data;
using QAAutomation.API.DTOs;
using QAAutomation.API.Models;

namespace QAAutomation.API.Services;

/// <summary>Service that manages knowledge base sources and documents, and retrieves relevant context for RAG.</summary>
public interface IKnowledgeBaseService
{
    // Sources
    Task<List<KnowledgeSourceDto>> GetSourcesAsync();
    Task<KnowledgeSourceDto?> GetSourceAsync(int id);
    Task<KnowledgeSourceDto> CreateSourceAsync(KnowledgeSourceDto dto);
    Task<KnowledgeSourceDto?> UpdateSourceAsync(int id, KnowledgeSourceDto dto);
    Task<bool> DeleteSourceAsync(int id);

    // Documents
    Task<List<KnowledgeDocumentDto>> GetDocumentsAsync(int? sourceId = null);
    Task<KnowledgeDocumentDto> AddDocumentAsync(KnowledgeDocumentUploadDto dto);
    Task<bool> DeleteDocumentAsync(int id);

    // RAG retrieval
    /// <summary>Returns up to topK most relevant document excerpts for the given query text.</summary>
    Task<List<string>> RetrieveAsync(string query, int topK = 3, string? tags = null);
}

/// <summary>
/// Knowledge-base service backed by SQLite.
/// Uses BM25-inspired keyword scoring for retrieval — no external vector store required.
/// Supports manual upload today; Azure Blob, SFTP, and SharePoint connectors sync on demand.
/// </summary>
public class KnowledgeBaseService : IKnowledgeBaseService
{
    private readonly AppDbContext _db;
    private readonly ILogger<KnowledgeBaseService> _logger;

    public KnowledgeBaseService(AppDbContext db, ILogger<KnowledgeBaseService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ── Sources ───────────────────────────────────────────────────────────────

    public async Task<List<KnowledgeSourceDto>> GetSourcesAsync()
    {
        var sources = await _db.KnowledgeSources
            .Include(s => s.Documents)
            .OrderBy(s => s.Name)
            .ToListAsync();
        return sources.Select(s => ToDto(s, s.Documents.Count)).ToList();
    }

    public async Task<KnowledgeSourceDto?> GetSourceAsync(int id)
    {
        var s = await _db.KnowledgeSources.Include(s => s.Documents).FirstOrDefaultAsync(s => s.Id == id);
        return s == null ? null : ToDto(s, s.Documents.Count);
    }

    public async Task<KnowledgeSourceDto> CreateSourceAsync(KnowledgeSourceDto dto)
    {
        var entity = FromDto(dto);
        entity.CreatedAt = DateTime.UtcNow;
        _db.KnowledgeSources.Add(entity);
        await _db.SaveChangesAsync();
        return ToDto(entity, 0);
    }

    public async Task<KnowledgeSourceDto?> UpdateSourceAsync(int id, KnowledgeSourceDto dto)
    {
        var entity = await _db.KnowledgeSources.Include(s => s.Documents).FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) return null;
        entity.Name = dto.Name;
        entity.ConnectorType = dto.ConnectorType;
        entity.Description = dto.Description;
        entity.BlobConnectionString = dto.BlobConnectionString;
        entity.BlobContainerName = dto.BlobContainerName;
        entity.SftpHost = dto.SftpHost;
        entity.SftpPort = dto.SftpPort;
        entity.SftpUsername = dto.SftpUsername;
        entity.SftpPassword = dto.SftpPassword;
        entity.SftpPath = dto.SftpPath;
        entity.SharePointSiteUrl = dto.SharePointSiteUrl;
        entity.SharePointClientId = dto.SharePointClientId;
        entity.SharePointClientSecret = dto.SharePointClientSecret;
        entity.SharePointLibraryName = dto.SharePointLibraryName;
        entity.IsActive = dto.IsActive;
        await _db.SaveChangesAsync();
        return ToDto(entity, entity.Documents.Count);
    }

    public async Task<bool> DeleteSourceAsync(int id)
    {
        var entity = await _db.KnowledgeSources.FindAsync(id);
        if (entity == null) return false;
        _db.KnowledgeSources.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Documents ─────────────────────────────────────────────────────────────

    public async Task<List<KnowledgeDocumentDto>> GetDocumentsAsync(int? sourceId = null)
    {
        var q = _db.KnowledgeDocuments.Include(d => d.Source).AsQueryable();
        if (sourceId.HasValue) q = q.Where(d => d.SourceId == sourceId.Value);
        return await q.OrderByDescending(d => d.UploadedAt)
                      .Select(d => ToDocDto(d))
                      .ToListAsync();
    }

    public async Task<KnowledgeDocumentDto> AddDocumentAsync(KnowledgeDocumentUploadDto dto)
    {
        var content = dto.Content.Trim();
        var entity = new KnowledgeDocument
        {
            SourceId = dto.SourceId,
            Title = dto.Title,
            FileName = dto.FileName,
            Content = content,
            Tags = dto.Tags,
            ContentSizeBytes = System.Text.Encoding.UTF8.GetByteCount(content),
            UploadedAt = DateTime.UtcNow
        };
        _db.KnowledgeDocuments.Add(entity);
        // Bump LastSyncedAt on source
        var source = await _db.KnowledgeSources.FindAsync(dto.SourceId);
        if (source != null) source.LastSyncedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _db.Entry(entity).Reference(e => e.Source).LoadAsync();
        return ToDocDto(entity);
    }

    public async Task<bool> DeleteDocumentAsync(int id)
    {
        var entity = await _db.KnowledgeDocuments.FindAsync(id);
        if (entity == null) return false;
        _db.KnowledgeDocuments.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }

    // ── RAG Retrieval (BM25-style keyword scoring) ────────────────────────────

    /// <summary>Maximum character length of each retrieved document snippet injected into the LLM prompt.
    /// Kept short enough to stay within typical context windows while still conveying the policy text.</summary>
    private const int RagSnippetLength = 600;

    public async Task<List<string>> RetrieveAsync(string query, int topK = 3, string? tags = null)
    {
        if (string.IsNullOrWhiteSpace(query)) return new List<string>();

        var queryTerms = Tokenize(query);
        if (queryTerms.Count == 0) return new List<string>();

        var docsQuery = _db.KnowledgeDocuments
            .Include(d => d.Source)
            .Where(d => d.Source.IsActive);

        var docs = await docsQuery.ToListAsync();

        // Filter by tags in memory to avoid SQL translation issues
        if (!string.IsNullOrWhiteSpace(tags))
        {
            var tagList = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                              .Select(t => t.ToLowerInvariant())
                              .ToList();
            docs = docs.Where(d => d.Tags == null ||
                tagList.Any(t => d.Tags.ToLowerInvariant().Contains(t))).ToList();
        }

        // Score each document by term frequency of query tokens
        var scored = docs.Select(doc =>
        {
            var lowerContent = doc.Content.ToLowerInvariant();
            var lowerTitle = doc.Title.ToLowerInvariant();
            double score = 0;
            foreach (var term in queryTerms)
            {
                // Title match counts 3x
                score += CountOccurrences(lowerTitle, term) * 3.0;
                score += CountOccurrences(lowerContent, term) * 1.0;
            }
            return (doc, score);
        })
        .Where(x => x.score > 0)
        .OrderByDescending(x => x.score)
        .Take(topK)
        .ToList();

        return scored.Select(x =>
        {
            var snippet = x.doc.Content.Length > RagSnippetLength
                ? x.doc.Content[..RagSnippetLength] + "..."
                : x.doc.Content;
            return $"[{x.doc.Title}]\n{snippet}";
        }).ToList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static List<string> Tokenize(string text) =>
        text.ToLowerInvariant()
            .Split(new[] { ' ', '\t', '\n', '\r', ',', '.', ';', ':', '(', ')', '-', '_' },
                   StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 3)
            .Distinct()
            .ToList();

    private static int CountOccurrences(string text, string term)
    {
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(term, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += term.Length;
        }
        return count;
    }

    private static KnowledgeSourceDto ToDto(KnowledgeSource s, int docCount) => new()
    {
        Id = s.Id,
        Name = s.Name,
        ConnectorType = s.ConnectorType,
        Description = s.Description,
        BlobConnectionString = s.BlobConnectionString,
        BlobContainerName = s.BlobContainerName,
        SftpHost = s.SftpHost,
        SftpPort = s.SftpPort,
        SftpUsername = s.SftpUsername,
        SftpPassword = s.SftpPassword,
        SftpPath = s.SftpPath,
        SharePointSiteUrl = s.SharePointSiteUrl,
        SharePointClientId = s.SharePointClientId,
        SharePointClientSecret = s.SharePointClientSecret,
        SharePointLibraryName = s.SharePointLibraryName,
        IsActive = s.IsActive,
        CreatedAt = s.CreatedAt,
        LastSyncedAt = s.LastSyncedAt,
        DocumentCount = docCount
    };

    private static KnowledgeSource FromDto(KnowledgeSourceDto dto) => new()
    {
        Name = dto.Name,
        ConnectorType = dto.ConnectorType,
        Description = dto.Description,
        BlobConnectionString = dto.BlobConnectionString,
        BlobContainerName = dto.BlobContainerName,
        SftpHost = dto.SftpHost,
        SftpPort = dto.SftpPort,
        SftpUsername = dto.SftpUsername,
        SftpPassword = dto.SftpPassword,
        SftpPath = dto.SftpPath,
        SharePointSiteUrl = dto.SharePointSiteUrl,
        SharePointClientId = dto.SharePointClientId,
        SharePointClientSecret = dto.SharePointClientSecret,
        SharePointLibraryName = dto.SharePointLibraryName,
        IsActive = dto.IsActive
    };

    private static KnowledgeDocumentDto ToDocDto(KnowledgeDocument d) => new()
    {
        Id = d.Id,
        SourceId = d.SourceId,
        SourceName = d.Source?.Name ?? "",
        Title = d.Title,
        FileName = d.FileName,
        Content = d.Content,
        Tags = d.Tags,
        ContentSizeBytes = d.ContentSizeBytes,
        UploadedAt = d.UploadedAt
    };
}
