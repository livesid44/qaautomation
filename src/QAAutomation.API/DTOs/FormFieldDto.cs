using QAAutomation.API.Models;

namespace QAAutomation.API.DTOs;

public class FormFieldDto
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public FieldType FieldType { get; set; }
    public bool IsRequired { get; set; }
    public int Order { get; set; }
    public string? Options { get; set; }
    public int MaxRating { get; set; }
    public int SectionId { get; set; }
}

public class CreateFormFieldDto
{
    public string Label { get; set; } = string.Empty;
    public FieldType FieldType { get; set; }
    public bool IsRequired { get; set; }
    public int Order { get; set; }
    public string? Options { get; set; }
    public int MaxRating { get; set; } = 5;
}

public class UpdateFormFieldDto
{
    public string Label { get; set; } = string.Empty;
    public FieldType FieldType { get; set; }
    public bool IsRequired { get; set; }
    public int Order { get; set; }
    public string? Options { get; set; }
    public int MaxRating { get; set; } = 5;
}
