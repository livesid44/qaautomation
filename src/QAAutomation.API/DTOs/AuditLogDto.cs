namespace QAAutomation.API.DTOs;

/// <summary>A single audit log entry returned by the API.</summary>
public class AuditLogEntryDto
{
    public int Id { get; set; }
    public int? ProjectId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string? Actor { get; set; }
    public string? PiiTypesDetected { get; set; }
    public string? HttpMethod { get; set; }
    public string? Endpoint { get; set; }
    public int? HttpStatusCode { get; set; }
    public long? DurationMs { get; set; }
    public string? Provider { get; set; }
    public string? Details { get; set; }
    public DateTime OccurredAt { get; set; }
}

/// <summary>Paginated audit log response.</summary>
public class AuditLogPageDto
{
    public List<AuditLogEntryDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 1;
}
