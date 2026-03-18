using System.ComponentModel.DataAnnotations;

namespace QAAutomation.API.Models;

public class FormField
{
    public int Id { get; set; }

    [Required]
    public string Label { get; set; } = string.Empty;

    public FieldType FieldType { get; set; }

    public bool IsRequired { get; set; }

    public int Order { get; set; }

    /// <summary>JSON serialized list of options for Dropdown fields.</summary>
    public string? Options { get; set; }

    /// <summary>Maximum rating value for Rating fields. Defaults to 5.</summary>
    public int MaxRating { get; set; } = 5;

    /// <summary>Assessment guideline / description shown to evaluators on the audit form.</summary>
    public string? Description { get; set; }

    public int SectionId { get; set; }

    public FormSection Section { get; set; } = null!;
}
