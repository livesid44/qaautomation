using System.ComponentModel.DataAnnotations;

namespace QAAutomation.Web.Models;

public class LoginViewModel
{
    [Required] public string Username { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class DashboardViewModel
{
    public int ParameterCount { get; set; }
    public int ParameterClubCount { get; set; }
    public int RatingCriteriaCount { get; set; }
    public int EvaluationFormCount { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class UserViewModel
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateUserViewModel
{
    [Required] public string Username { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; } = true;
}

public class EditUserViewModel
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; }
    public string? Password { get; set; }
}

public class ParameterViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public double DefaultWeight { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateParameterViewModel
{
    [Required] public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public double DefaultWeight { get; set; } = 1.0;
    public bool IsActive { get; set; } = true;
}

public class RatingLevelViewModel
{
    public int Id { get; set; }
    public int CriteriaId { get; set; }
    public int Score { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#6c757d";
}

public class RatingCriteriaViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int MinScore { get; set; }
    public int MaxScore { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<RatingLevelViewModel> Levels { get; set; } = new();
}

public class ParameterClubItemViewModel
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

public class ParameterClubViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ParameterClubItemViewModel> Items { get; set; } = new();
}

public class EvaluationFormFieldViewModel
{
    public int Id { get; set; }
    public int ParameterId { get; set; }
    public string ParameterName { get; set; } = string.Empty;
    public int? RatingCriteriaId { get; set; }
    public string? RatingCriteriaName { get; set; }
    public int Order { get; set; }
}

public class EvaluationFormSectionViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int? ClubId { get; set; }
    public int Order { get; set; }
    public List<EvaluationFormFieldViewModel> Fields { get; set; } = new();
}

public class EvaluationFormViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<EvaluationFormSectionViewModel> Sections { get; set; } = new();
}

public class FormDesignerViewModel
{
    public EvaluationFormViewModel Form { get; set; } = new();
    public List<ParameterClubViewModel> AvailableClubs { get; set; } = new();
    public List<ParameterViewModel> AllParameters { get; set; } = new();
    public List<RatingCriteriaViewModel> AllCriteria { get; set; } = new();
}
