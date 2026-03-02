namespace QAAutomation.API.DTOs;

public class RatingLevelDto
{
    public int Id { get; set; }
    public int CriteriaId { get; set; }
    public int Score { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#6c757d";
}

public class CreateRatingLevelDto
{
    public int Score { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#6c757d";
}

public class RatingCriteriaDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int MinScore { get; set; }
    public int MaxScore { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<RatingLevelDto> Levels { get; set; } = new();
}

public class CreateRatingCriteriaDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int MinScore { get; set; } = 1;
    public int MaxScore { get; set; } = 5;
    public int? ProjectId { get; set; }
    public List<CreateRatingLevelDto> Levels { get; set; } = new();
}

public class UpdateRatingCriteriaDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int MinScore { get; set; }
    public int MaxScore { get; set; }
    public bool IsActive { get; set; }
    public List<CreateRatingLevelDto> Levels { get; set; } = new();
}
