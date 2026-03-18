namespace QAAutomation.API.DTOs;

public class ParameterDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public double DefaultWeight { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string EvaluationType { get; set; } = "LLM";
    public int? ProjectId { get; set; }
}

public class CreateParameterDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public double DefaultWeight { get; set; } = 1.0;
    public string EvaluationType { get; set; } = "LLM";
    public int? ProjectId { get; set; }
}

public class UpdateParameterDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public double DefaultWeight { get; set; }
    public bool IsActive { get; set; }
    public string EvaluationType { get; set; } = "LLM";
}
