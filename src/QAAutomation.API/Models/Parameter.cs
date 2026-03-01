using System.ComponentModel.DataAnnotations;
namespace QAAutomation.API.Models;
public class Parameter {
    public int Id { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public double DefaultWeight { get; set; } = 1.0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    /// <summary>"LLM" (soft-skill, scored directly by the LLM) or "KnowledgeBased" (process param scored with RAG context).</summary>
    public string EvaluationType { get; set; } = "LLM";
}
