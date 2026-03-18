using System.ComponentModel.DataAnnotations;

namespace QAAutomation.API.Models;

/// <summary>
/// Top-level tenant. Everything (parameters, forms, audits, KB) is scoped under a Project.
/// Hierarchy: Project → LOB → EvaluationForm.
/// </summary>
public class Project
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Lob> Lobs { get; set; } = new List<Lob>();
    public ICollection<UserProjectAccess> UserAccess { get; set; } = new List<UserProjectAccess>();
}
