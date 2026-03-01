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
    public int AuditCount { get; set; }
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

// Legacy form models for Audit (maps to EvaluationFormDto with FormSection/FormField structure)
public class LegacyFormFieldViewModel
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public int MaxRating { get; set; }
    public bool IsRequired { get; set; }
    public int FieldType { get; set; } // 2 = Rating
    public int SectionId { get; set; }
}

public class LegacySectionViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Order { get; set; }
    public List<LegacyFormFieldViewModel> Fields { get; set; } = new();
}

public class LegacyFormViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public List<LegacySectionViewModel> Sections { get; set; } = new();
}

// Audit view models
public class AuditScoreViewModel
{
    public int FieldId { get; set; }
    public string FieldLabel { get; set; } = string.Empty;
    public int MaxRating { get; set; }
    public bool IsRequired { get; set; }
    public string Value { get; set; } = string.Empty;
    public double? NumericValue { get; set; }
}

public class AuditSectionViewModel
{
    public string Title { get; set; } = string.Empty;
    public List<AuditScoreViewModel> Fields { get; set; } = new();
}

public class AuditViewModel
{
    public int Id { get; set; }
    public int FormId { get; set; }
    public string FormName { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string EvaluatedBy { get; set; } = string.Empty;
    public string? CallReference { get; set; }
    public DateTime? CallDate { get; set; }
    public DateTime EvaluatedAt { get; set; }
    public string? Notes { get; set; }
    public double TotalScore { get; set; }
    public double MaxPossibleScore { get; set; }
    public double ScorePercent => MaxPossibleScore > 0 ? Math.Round(TotalScore / MaxPossibleScore * 100, 1) : 0;
    public List<AuditSectionViewModel> Sections { get; set; } = new();
}

public class NewAuditViewModel
{
    [Required] public int FormId { get; set; }
    [Required] public string AgentName { get; set; } = string.Empty;
    [Required] public string EvaluatedBy { get; set; } = string.Empty;
    public string? CallReference { get; set; }
    public DateTime? CallDate { get; set; }
    public string? Notes { get; set; }
}

public class NewAuditFormViewModel
{
    public List<EvaluationFormViewModel> Forms { get; set; } = new();
}

// ──────────────────────────────────────────────────────────────────────────────
// Auto Audit (transcript upload + LLM scoring) view models
// ──────────────────────────────────────────────────────────────────────────────

public class AutoAuditUploadViewModel
{
    [Required] public int FormId { get; set; }
    [Required] public string EvaluatedBy { get; set; } = string.Empty;
    public string? AgentName { get; set; }
    public string? CallReference { get; set; }
    public DateTime? CallDate { get; set; }
    /// <summary>Pasted transcript text (alternative to file upload).</summary>
    public string? TranscriptText { get; set; }
}

public class AutoAuditFieldReviewViewModel
{
    public int FieldId { get; set; }
    public string SectionTitle { get; set; } = string.Empty;
    public string FieldLabel { get; set; } = string.Empty;
    public int MaxRating { get; set; }
    public double SuggestedScore { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    /// <summary>Final score (may be adjusted by the reviewer).</summary>
    public double FinalScore { get; set; }
}

public class AutoAuditReviewViewModel
{
    public int FormId { get; set; }
    public string FormName { get; set; } = string.Empty;
    public string Transcript { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string EvaluatedBy { get; set; } = string.Empty;
    public string? CallReference { get; set; }
    public DateTime? CallDate { get; set; }
    public string OverallReasoning { get; set; } = string.Empty;
    public bool IsAiGenerated { get; set; }
    public string? AnalysisError { get; set; }
    public List<AutoAuditFieldReviewViewModel> Fields { get; set; } = new();
    public double TotalScore => Fields.Sum(f => f.FinalScore);
    public double MaxPossibleScore => Fields.Sum(f => f.MaxRating);
    public double ScorePercent => MaxPossibleScore > 0 ? Math.Round(TotalScore / MaxPossibleScore * 100, 1) : 0;
}
