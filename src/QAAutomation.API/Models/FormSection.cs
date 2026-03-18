using System.ComponentModel.DataAnnotations;

namespace QAAutomation.API.Models;

public class FormSection
{
    public int Id { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int Order { get; set; }

    public int FormId { get; set; }

    public EvaluationForm Form { get; set; } = null!;

    public ICollection<FormField> Fields { get; set; } = new List<FormField>();
}
