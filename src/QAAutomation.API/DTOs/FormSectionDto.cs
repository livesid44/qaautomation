namespace QAAutomation.API.DTOs;

public class FormSectionDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Order { get; set; }
    public int FormId { get; set; }
    public List<FormFieldDto> Fields { get; set; } = new();
}

public class CreateFormSectionDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Order { get; set; }
    public List<CreateFormFieldDto> Fields { get; set; } = new();
}

public class UpdateFormSectionDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Order { get; set; }
}
