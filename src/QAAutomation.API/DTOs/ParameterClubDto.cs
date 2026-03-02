namespace QAAutomation.API.DTOs;

public class ParameterClubItemDto
{
    public int Id { get; set; }
    public int ClubId { get; set; }
    public int ParameterId { get; set; }
    public string ParameterName { get; set; } = string.Empty;
    public int Order { get; set; }
    public double? WeightOverride { get; set; }
    public int? RatingCriteriaId { get; set; }
    public string? RatingCriteriaName { get; set; }
}

public class ParameterClubDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ParameterClubItemDto> Items { get; set; } = new();
}

public class CreateParameterClubDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ProjectId { get; set; }
    public List<UpdateClubItemDto> Items { get; set; } = new();
}

public class UpdateParameterClubDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class UpdateClubItemDto
{
    public int ParameterId { get; set; }
    public int Order { get; set; }
    public double? WeightOverride { get; set; }
    public int? RatingCriteriaId { get; set; }
}
