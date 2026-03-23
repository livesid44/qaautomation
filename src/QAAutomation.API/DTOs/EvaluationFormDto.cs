using QAAutomation.API.Models;

namespace QAAutomation.API.DTOs;

public class EvaluationFormDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; }
    public int? LobId { get; set; }
    public string? LobName { get; set; }
    public int? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public ScoringMethod ScoringMethod { get; set; } = ScoringMethod.Generic;
    public List<FormSectionDto> Sections { get; set; } = new();
}

public class CreateEvaluationFormDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? LobId { get; set; }
    public List<CreateFormSectionDto> Sections { get; set; } = new();
}

public class UpdateEvaluationFormDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int? LobId { get; set; }
}
