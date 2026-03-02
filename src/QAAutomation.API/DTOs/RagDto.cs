using System.ComponentModel.DataAnnotations;

namespace QAAutomation.API.DTOs;

public class AiConfigDto
{
    // LLM
    public string LlmProvider { get; set; } = "AzureOpenAI";
    public string LlmEndpoint { get; set; } = string.Empty;
    public string LlmApiKey { get; set; } = string.Empty;
    public string LlmDeployment { get; set; } = "gpt-4o";
    public float LlmTemperature { get; set; } = 0.1f;

    // Sentiment
    public string SentimentProvider { get; set; } = "AzureOpenAI";
    public string LanguageEndpoint { get; set; } = string.Empty;
    public string LanguageApiKey { get; set; } = string.Empty;

    // RAG
    public int RagTopK { get; set; } = 3;

    public DateTime UpdatedAt { get; set; }
}

public class KnowledgeSourceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ConnectorType { get; set; } = "ManualUpload";
    public string? Description { get; set; }
    public string? BlobConnectionString { get; set; }
    public string? BlobContainerName { get; set; }
    public string? SftpHost { get; set; }
    public int? SftpPort { get; set; }
    public string? SftpUsername { get; set; }
    public string? SftpPassword { get; set; }
    public string? SftpPath { get; set; }
    public string? SharePointSiteUrl { get; set; }
    public string? SharePointClientId { get; set; }
    public string? SharePointClientSecret { get; set; }
    public string? SharePointLibraryName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public int DocumentCount { get; set; }
    public int? ProjectId { get; set; }
}

public class KnowledgeDocumentDto
{
    public int Id { get; set; }
    public int SourceId { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Tags { get; set; }
    public long ContentSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class KnowledgeDocumentUploadDto
{
    [Required] public int SourceId { get; set; }
    [Required] public string Title { get; set; } = string.Empty;
    public string? FileName { get; set; }
    [Required] public string Content { get; set; } = string.Empty;
    public string? Tags { get; set; }
}
