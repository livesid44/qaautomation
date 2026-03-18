using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using QAAutomation.API.Data;
using QAAutomation.API.DTOs;
using QAAutomation.API.Models;

namespace QAAutomation.API.Services;

/// <summary>Service that manages knowledge base sources and documents, and retrieves relevant context for RAG.</summary>
public interface IKnowledgeBaseService
{
    // Sources
    Task<List<KnowledgeSourceDto>> GetSourcesAsync(int? projectId = null);
    Task<KnowledgeSourceDto?> GetSourceAsync(int id);
    Task<KnowledgeSourceDto> CreateSourceAsync(KnowledgeSourceDto dto);
    Task<KnowledgeSourceDto?> UpdateSourceAsync(int id, KnowledgeSourceDto dto);
    Task<bool> DeleteSourceAsync(int id);

    // Documents
    Task<List<KnowledgeDocumentDto>> GetDocumentsAsync(int? sourceId = null);
    Task<KnowledgeDocumentDto> AddDocumentAsync(KnowledgeDocumentUploadDto dto);
    /// <summary>Fetches a public URL, extracts readable text, and stores it as a KB document.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the URL cannot be fetched or content extraction fails.</exception>
    Task<KnowledgeDocumentDto> FetchUrlAsync(KnowledgeUrlFetchDto dto);
    Task<bool> DeleteDocumentAsync(int id);

    // RAG retrieval
    /// <summary>Returns up to topK most relevant document excerpts for the given query text,
    /// scoped to the specified project when <paramref name="projectId"/> is provided.</summary>
    Task<List<string>> RetrieveAsync(string query, int topK = 3, string? tags = null, int? projectId = null);
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
    private readonly IHttpClientFactory _httpClientFactory;

    public KnowledgeBaseService(AppDbContext db, ILogger<KnowledgeBaseService> logger, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    // ── Sources ───────────────────────────────────────────────────────────────

    public async Task<List<KnowledgeSourceDto>> GetSourcesAsync(int? projectId = null)
    {
        var query = _db.KnowledgeSources
            .Include(s => s.Documents)
            .OrderBy(s => s.Name)
            .AsQueryable();
        if (projectId.HasValue)
            query = query.Where(s => s.ProjectId == projectId.Value);
        var sources = await query.ToListAsync();
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

    // ── URL fetching ──────────────────────────────────────────────────────────

    /// <summary>Maximum characters retained from a fetched web page.</summary>
    private const int MaxFetchedChars = 50_000;

    private const string TruncationMarker = "\n[CONTENT TRUNCATED]";

    /// <summary>Maximum response body size read from a remote URL (10 MB).</summary>
    private const int MaxResponseBytes = 10 * 1024 * 1024;

    public async Task<KnowledgeDocumentDto> FetchUrlAsync(KnowledgeUrlFetchDto dto)
    {
        // Validate scheme — only public http/https is permitted
        if (!Uri.TryCreate(dto.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException($"Invalid or non-HTTP URL: {dto.Url}");

        // SSRF guard — reject localhost and known private network ranges
        if (IsPrivateOrLocalHost(uri.Host))
            throw new InvalidOperationException("Requests to private or loopback addresses are not permitted.");

        string rawHtml;
        try
        {
            var client   = _httpClientFactory.CreateClient("kb-url-fetch");
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Remote server returned HTTP {(int)response.StatusCode} {response.StatusCode}.");

            // Reject non-text responses (binary files, JSON APIs, etc.)
            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"URL returned unsupported content type '{contentType}'. Only text/html pages are supported.");

            // Guard against unexpectedly large responses
            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength.HasValue && contentLength.Value > MaxResponseBytes)
                throw new InvalidOperationException(
                    $"Remote page is too large ({contentLength.Value / 1024 / 1024} MB). Maximum allowed is 10 MB.");

            rawHtml = await response.Content.ReadAsStringAsync();
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch URL {Url}", dto.Url);
            throw new InvalidOperationException($"Could not fetch URL: {ex.Message}");
        }

        var text = ExtractTextFromHtml(rawHtml);
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("No readable text content was found at the supplied URL.");

        if (text.Length > MaxFetchedChars)
            text = text[..MaxFetchedChars] + TruncationMarker;

        var title = string.IsNullOrWhiteSpace(dto.Title) ? dto.Url : dto.Title.Trim();
        var uploadDto = new KnowledgeDocumentUploadDto
        {
            SourceId = dto.SourceId,
            Title    = title,
            FileName = dto.Url,
            Content  = text,
            Tags     = dto.Tags
        };
        return await AddDocumentAsync(uploadDto);
    }

    /// <summary>
    /// Returns <c>true</c> when the hostname resolves to a private, loopback, or link-local
    /// address — i.e. when the request should be blocked to prevent SSRF.
    /// </summary>
    internal static bool IsPrivateOrLocalHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host)) return true;

        var lower = host.ToLowerInvariant().TrimEnd('.');

        // Block loopback hostnames
        if (lower is "localhost" or "ip6-localhost" or "ip6-loopback") return true;

        // If the host is already an IP address, inspect it directly
        if (System.Net.IPAddress.TryParse(lower, out var ip))
            return IsPrivateIp(ip);

        // For hostnames we cannot do a DNS pre-flight without introducing TOCTOU risk,
        // so we only reject the well-known private hostnames above.
        return false;
    }

    private static bool IsPrivateIp(System.Net.IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();

        // IPv4
        if (bytes.Length == 4)
        {
            return (bytes[0] == 127)                                          // 127.0.0.0/8  loopback
                || (bytes[0] == 10)                                           // 10.0.0.0/8
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)     // 172.16.0.0/12
                || (bytes[0] == 192 && bytes[1] == 168)                      // 192.168.0.0/16
                || (bytes[0] == 169 && bytes[1] == 254);                     // 169.254.0.0/16 link-local
        }

        // IPv6 loopback (::1)
        if (bytes.Length == 16)
            return ip.Equals(System.Net.IPAddress.IPv6Loopback)
                || ip.Equals(System.Net.IPAddress.IPv6Any);

        return false;
    }

    /// <summary>
    /// Strips HTML markup from a raw HTML string and returns clean readable text.
    /// Removes script/style blocks first, then strips all remaining tags.
    /// </summary>
    internal static string ExtractTextFromHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        // Remove HTML comments
        var cleaned = Regex.Replace(html, @"<!--[\s\S]*?-->", string.Empty);

        // Remove script and style elements including their content
        cleaned = Regex.Replace(cleaned, @"<(script|style)[^>]*>[\s\S]*?</(script|style)>",
            string.Empty, RegexOptions.IgnoreCase);

        // Strip all remaining tags
        cleaned = Regex.Replace(cleaned, @"<[^>]+>", " ");

        // Decode HTML entities (&amp; &nbsp; etc.)
        cleaned = WebUtility.HtmlDecode(cleaned);

        // Collapse whitespace
        cleaned = Regex.Replace(cleaned, @"[ \t]+", " ");
        cleaned = Regex.Replace(cleaned, @"\n{3,}", "\n\n");

        return cleaned.Trim();
    }

    // ── RAG Retrieval (BM25-style keyword scoring) ────────────────────────────

    /// <summary>Maximum character length of each retrieved document snippet injected into the LLM prompt.
    /// Kept short enough to stay within typical context windows while still conveying the policy text.</summary>
    private const int RagSnippetLength = 600;

    public async Task<List<string>> RetrieveAsync(string query, int topK = 3, string? tags = null, int? projectId = null)
    {
        if (string.IsNullOrWhiteSpace(query)) return new List<string>();

        var queryTerms = Tokenize(query);
        if (queryTerms.Count == 0) return new List<string>();

        var docsQuery = _db.KnowledgeDocuments
            .Include(d => d.Source)
            .Where(d => d.Source.IsActive);

        // Scope to the current project so tenants never see each other's KB documents
        if (projectId.HasValue)
            docsQuery = docsQuery.Where(d => d.Source.ProjectId == projectId.Value);

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
        DocumentCount = docCount,
        ProjectId = s.ProjectId
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
        IsActive = dto.IsActive,
        ProjectId = dto.ProjectId
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
