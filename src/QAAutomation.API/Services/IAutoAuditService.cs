using QAAutomation.API.DTOs;

namespace QAAutomation.API.Services;

/// <summary>Service that analyzes a call transcript against a QA evaluation form using an LLM.</summary>
public interface IAutoAuditService
{
    /// <summary>
    /// Analyzes the provided transcript against the given form fields and returns
    /// AI-generated scores and reasoning for each field.
    /// </summary>
    Task<AutoAuditResponseDto> AnalyzeTranscriptAsync(
        AutoAuditRequestDto request,
        IEnumerable<AutoAuditFieldDefinition> fields,
        string formName,
        int? projectId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Describes a single form field for the LLM scoring prompt.</summary>
public record AutoAuditFieldDefinition(
    int FieldId,
    string Label,
    string? Description,
    int MaxRating,
    bool IsRequired,
    string SectionTitle,
    string EvaluationType = "LLM");
