using System.ComponentModel.DataAnnotations;

namespace QAAutomation.API.Models;

/// <summary>
/// Line of Business — sits beneath a Project and groups related EvaluationForms.
/// Hierarchy: Project → LOB → EvaluationForm.
/// </summary>
public class Lob
{
    public int Id { get; set; }

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<EvaluationForm> EvaluationForms { get; set; } = new List<EvaluationForm>();
}
