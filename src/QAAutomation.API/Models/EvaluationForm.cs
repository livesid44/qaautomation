using System.ComponentModel.DataAnnotations;

namespace QAAutomation.API.Models;

public class EvaluationForm
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>LOB this form belongs to. Null only for legacy data during migration.</summary>
    public int? LobId { get; set; }
    public Lob? Lob { get; set; }

    public ICollection<FormSection> Sections { get; set; } = new List<FormSection>();
}
