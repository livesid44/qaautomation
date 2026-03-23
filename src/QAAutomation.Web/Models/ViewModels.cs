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
    public int CurrentProjectId { get; set; }
    public string CurrentProjectName { get; set; } = string.Empty;
    public string CurrentLobName { get; set; } = string.Empty;
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
    public string EvaluationType { get; set; } = "LLM";
}

public class CreateParameterViewModel
{
    [Required] public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public double DefaultWeight { get; set; } = 1.0;
    public bool IsActive { get; set; } = true;
    public string EvaluationType { get; set; } = "LLM";
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
    public string? Description { get; set; }
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
    /// <summary>Scoring method: 0 = Generic, 1 = SectionAutoFail.</summary>
    public int ScoringMethod { get; set; } = 0;
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

    // AI-generated data stored at audit-save time (null for manually-entered audits)
    public string? OverallReasoning { get; set; }
    public string? SentimentJson { get; set; }
    public string? FieldReasoningJson { get; set; }

    /// <summary>Deserialized sentiment — populated by ApiClient after fetching from the API.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public SentimentViewModel? Sentiment { get; set; }

    /// <summary>Map of FieldId → AI reasoning string — populated by ApiClient.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Dictionary<int, string> FieldReasonings { get; set; } = new();
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
    /// <summary>Scoring method: 0 = Generic, 1 = SectionAutoFail.</summary>
    public int ScoringMethod { get; set; } = 0;
    public double TotalScore => Fields.Sum(f => f.FinalScore);
    public double MaxPossibleScore => Fields.Sum(f => f.MaxRating);
    public double ScorePercent => MaxPossibleScore > 0 ? Math.Round(TotalScore / MaxPossibleScore * 100, 1) : 0;
    /// <summary>Sentiment and emotion analysis results (populated in parallel with quality scoring).</summary>
    public SentimentViewModel? Sentiment { get; set; }
}

// ──────────────────────────────────────────────────────────────────────────────
// Sentiment, Emotion & Recommendations view models
// ──────────────────────────────────────────────────────────────────────────────

public class DetectedEmotionViewModel
{
    public string Emotion { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Speaker { get; set; } = string.Empty;
}

public class KeyMomentViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Sentiment { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
}

public class CoachingRecommendationViewModel
{
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
}

public class SentimentViewModel
{
    public string OverallSentiment { get; set; } = string.Empty;
    public double OverallScore { get; set; }
    public string AgentSentiment { get; set; } = string.Empty;
    public double AgentScore { get; set; }
    public string CustomerSentiment { get; set; } = string.Empty;
    public double CustomerScore { get; set; }
    public string SentimentTrend { get; set; } = string.Empty;
    public List<DetectedEmotionViewModel> DominantEmotions { get; set; } = new();
    public List<KeyMomentViewModel> KeyMoments { get; set; } = new();
    public List<CoachingRecommendationViewModel> Recommendations { get; set; } = new();
    public string OverallInsight { get; set; } = string.Empty;
    public bool IsAiGenerated { get; set; }
    public string? AnalysisError { get; set; }
}

// ──────────────────────────────────────────────────────────────────────────────
// AI Settings view models
// ──────────────────────────────────────────────────────────────────────────────

public class AiSettingsViewModel
{
    public string LlmProvider { get; set; } = "AzureOpenAI";
    public string LlmEndpoint { get; set; } = string.Empty;
    public string LlmApiKey { get; set; } = string.Empty;
    public string LlmDeployment { get; set; } = "gpt-4o";
    public float LlmTemperature { get; set; } = 0.1f;
    public string SentimentProvider { get; set; } = "AzureOpenAI";
    public string LanguageEndpoint { get; set; } = string.Empty;
    public string LanguageApiKey { get; set; } = string.Empty;
    public int RagTopK { get; set; } = 3;
    // Speech-to-Text
    public string SpeechProvider { get; set; } = "Azure";
    public string SpeechEndpoint { get; set; } = string.Empty;
    public string SpeechApiKey { get; set; } = string.Empty;
    // Google (Gemini LLM + Cloud Speech-to-Text)
    public string GoogleApiKey { get; set; } = string.Empty;
    public string GoogleGeminiModel { get; set; } = "gemini-1.5-pro";
    public DateTime UpdatedAt { get; set; }
    /// <summary>True when an LLM (Azure/OpenAI or Google) is configured.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(LlmEndpoint) ||
                                (LlmProvider == "Google" && !string.IsNullOrWhiteSpace(GoogleApiKey));
    /// <summary>True when Speech-to-Text is configured.</summary>
    public bool IsSpeechConfigured => (SpeechProvider == "Azure" && !string.IsNullOrWhiteSpace(SpeechEndpoint)) ||
                                      (SpeechProvider == "Google" && !string.IsNullOrWhiteSpace(GoogleApiKey));
}

// ──────────────────────────────────────────────────────────────────────────────
// Knowledge Base view models
// ──────────────────────────────────────────────────────────────────────────────

public class LlmTestResultViewModel
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public long LatencyMs { get; set; }
}

public class KnowledgeSourceViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ConnectorType { get; set; } = "ManualUpload";
    public string? Description { get; set; }
    public string? BlobConnectionString { get; set; }
    public string? BlobContainerName { get; set; }
    public string? SftpHost { get; set; }
    public int? SftpPort { get; set; }
    public string? SftpUsername { get; set; }
    public string? SftpPassword { get; set; }
    public string? SftpPath { get; set; }
    public string? SharePointSiteUrl { get; set; }
    public string? SharePointClientId { get; set; }
    public string? SharePointClientSecret { get; set; }
    public string? SharePointLibraryName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public int DocumentCount { get; set; }
    public int? ProjectId { get; set; }
}

public class KnowledgeDocumentViewModel
{
    public int Id { get; set; }
    public int SourceId { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Tags { get; set; }
    public long ContentSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class KnowledgeDocumentUploadViewModel
{
    [Required] public int SourceId { get; set; }
    [Required] public string Title { get; set; } = string.Empty;
    public string? Tags { get; set; }
    public string? TextContent { get; set; }
}

public class KnowledgeUrlFetchViewModel
{
    [Required] public int SourceId { get; set; }
    [Required, Url] public string Url { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Tags { get; set; }
}

// ──────────────────────────────────────────────────────────────────────────────
// Multi-tenancy: Project & LOB view models
// ──────────────────────────────────────────────────────────────────────────────

public class ProjectViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int LobCount { get; set; }
    public bool PiiProtectionEnabled { get; set; }
    public string PiiRedactionMode { get; set; } = "Redact";
}

public class LobViewModel
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int FormCount { get; set; }
}

public class SelectProjectViewModel
{
    public List<ProjectViewModel> Projects { get; set; } = new();
    public string Username { get; set; } = string.Empty;
}

public class ProjectUserViewModel
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime GrantedAt { get; set; }
}


// ──────────────────────────────────────────────────────────────────────────────
// Analytics view models
// ──────────────────────────────────────────────────────────────────────────────

public class AnalyticsViewModel
{
    public int TotalAudits { get; set; }
    public List<DailyScoreViewModel> DailyScores { get; set; } = new();
    public List<AgentScoreViewModel> AgentScores { get; set; } = new();
    public List<ParameterTrendViewModel> ParameterTrends { get; set; } = new();
    public List<CallTypeScoreViewModel> CallTypeScores { get; set; } = new();
    public List<AgentDailyTrendViewModel> AgentDailyTrends { get; set; } = new();
    public List<SectionDailyTrendViewModel> SectionDailyTrends { get; set; } = new();
}

public class DailyScoreViewModel
{
    public string Date { get; set; } = string.Empty;
    public double AvgScorePercent { get; set; }
    public int AuditCount { get; set; }
}

public class AgentScoreViewModel
{
    public string AgentName { get; set; } = string.Empty;
    public double AvgScorePercent { get; set; }
    public int AuditCount { get; set; }
}

public class ParameterTrendViewModel
{
    public string ParameterLabel { get; set; } = string.Empty;
    public string SectionTitle { get; set; } = string.Empty;
    public double AvgScorePercent { get; set; }
    public int ScoredCount { get; set; }
}

public class CallTypeScoreViewModel
{
    public string FormName { get; set; } = string.Empty;
    public double AvgScorePercent { get; set; }
    public int AuditCount { get; set; }
}

public class AgentDailyTrendViewModel
{
    public string AgentName { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public double AvgScorePercent { get; set; }
    public int AuditCount { get; set; }
}

public class SectionDailyTrendViewModel
{
    public string SectionTitle { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public double AvgScorePercent { get; set; }
    public int ScoredCount { get; set; }
}

/// <summary>
/// AI-generated natural-language insights for the main Analytics Dashboard.
/// All fields are null when the LLM is not configured or there is insufficient data.
/// </summary>
public class AnalyticsInsightsViewModel
{
    public string? DailyTrendInsight { get; set; }
    public string? AgentPerformanceInsight { get; set; }
    public string? ParameterInsight { get; set; }
    public string? CallTypeInsight { get; set; }
}

// ── Explainability Analytics ViewModels ────────────────────────────────────────

public class ExplainabilityViewModel
{
    public int TotalAudits { get; set; }
    public int TotalReviewed { get; set; }
    public double AiHitlAgreementRate { get; set; }
    public List<DecisionDriverViewModel> DecisionDrivers { get; set; } = new();
    public List<SignalUsageViewModel> SignalUsage { get; set; } = new();
    public List<HitlAgreementViewModel> HitlAgreement { get; set; } = new();
    public List<FailureReasonViewModel> FailureReasons { get; set; } = new();
}

/// <summary>
/// AI-generated natural-language insights for each section of the Explainability page.
/// All fields are null when the LLM is not configured or there is insufficient data.
/// </summary>
public class ExplainabilityInsightsViewModel
{
    public string? DecisionDriversInsight { get; set; }
    public string? HitlAgreementInsight { get; set; }
    public string? SignalUsageInsight { get; set; }
    public string? FailureReasonsInsight { get; set; }
}

public class DecisionDriverViewModel
{
    public string ParameterLabel { get; set; } = string.Empty;
    public string SectionTitle { get; set; } = string.Empty;
    public double AvgScorePercent { get; set; }
    public int LowScoreCount { get; set; }
    public int HighScoreCount { get; set; }
    public int TotalScoredCount { get; set; }
    public double ScoreVariability { get; set; }
    public bool IsRiskArea { get; set; }
}

public class SignalUsageViewModel
{
    public string ParameterLabel { get; set; } = string.Empty;
    public string SectionTitle { get; set; } = string.Empty;
    public int TimesScored { get; set; }
    public int TimesFullScore { get; set; }
    public int TimesMissed { get; set; }
    public double FullScoreRate { get; set; }
    public double MissRate { get; set; }
}

public class HitlAgreementViewModel
{
    public string ReviewVerdict { get; set; } = string.Empty;
    public string PolicyName { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
}

public class FailureReasonViewModel
{
    public string ParameterLabel { get; set; } = string.Empty;
    public string SectionTitle { get; set; } = string.Empty;
    public int FailedAuditCount { get; set; }
    public double ContributionPercent { get; set; }
    public double AvgScoreInFailedAudits { get; set; }
}

// ── Audit Log ViewModels ────────────────────────────────────────────────────

public class AuditLogEntryViewModel
{
    public int Id { get; set; }
    public int? ProjectId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string? Actor { get; set; }
    public string? PiiTypesDetected { get; set; }
    public string? HttpMethod { get; set; }
    public string? Endpoint { get; set; }
    public int? HttpStatusCode { get; set; }
    public long? DurationMs { get; set; }
    public string? Provider { get; set; }
    public string? Details { get; set; }
    public DateTime OccurredAt { get; set; }
}

public class AuditLogPageViewModel
{
    public List<AuditLogEntryViewModel> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }

    // Filter state (for form repopulation)
    public string? FilterCategory { get; set; }
    public string? FilterEventType { get; set; }
    public string? FilterOutcome { get; set; }
    public string? FilterFrom { get; set; }
    public string? FilterTo { get; set; }
}

// ── Call Pipeline ViewModels ────────────────────────────────────────────────

public class CallPipelineJobViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public int FormId { get; set; }
    public string? FormName { get; set; }
    public int? ProjectId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int TotalItems { get; set; }
    public int CompletedItems { get; set; }
    public int FailedItems { get; set; }
    public List<CallPipelineItemViewModel> Items { get; set; } = new();
}

public class CallPipelineItemViewModel
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public string? SourceReference { get; set; }
    public string? AgentName { get; set; }
    public string? CallReference { get; set; }
    public DateTime? CallDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int? EvaluationResultId { get; set; }
    public double? ScorePercent { get; set; }
    public string? AiReasoning { get; set; }
}

/// <summary>Form model for submitting a batch of recording/transcript URLs.</summary>
public class CallPipelineBatchUrlViewModel
{
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public int FormId { get; set; }
    public int? ProjectId { get; set; }

    /// <summary>
    /// Newline-separated list of URLs.
    /// Each line can optionally include metadata: URL|agentName|callReference|callDate (ISO 8601)
    /// </summary>
    [Required]
    [Display(Name = "Recording / Transcript URLs (one per line)")]
    public string UrlList { get; set; } = string.Empty;
}

/// <summary>Form model for uploading a CSV / XLSX file of transcripts.</summary>
public class CallPipelineFileUploadViewModel
{
    [Required]
    [Display(Name = "Job Name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Evaluation Form")]
    public int FormId { get; set; }

    public int? ProjectId { get; set; }

    [Required]
    [Display(Name = "Transcript File (CSV or XLSX)")]
    public IFormFile? File { get; set; }
}

/// <summary>Form model for creating a connector-based pipeline job.</summary>
public class CallPipelineConnectorViewModel
{
    [Required] public string Name { get; set; } = string.Empty;

    /// <summary>"SFTP" | "SharePoint" | "Verint" | "NICE" | "Ozonetel"</summary>
    [Required] public string SourceType { get; set; } = "SFTP";

    [Required] public int FormId { get; set; }
    public int? ProjectId { get; set; }

    // SFTP
    public string? SftpHost { get; set; }
    public int? SftpPort { get; set; }
    public string? SftpUsername { get; set; }
    public string? SftpPassword { get; set; }
    public string? SftpPath { get; set; }

    // SharePoint
    public string? SharePointSiteUrl { get; set; }
    public string? SharePointClientId { get; set; }
    public string? SharePointClientSecret { get; set; }
    public string? SharePointLibraryName { get; set; }

    // Recording platforms
    public string? RecordingPlatformUrl { get; set; }
    public string? RecordingPlatformApiKey { get; set; }
    public string? RecordingPlatformTenantId { get; set; }

    // Date filter
    public string? FilterFromDate { get; set; }
    public string? FilterToDate { get; set; }
}

// ── Human Review / Sampling ViewModels ─────────────────────────────────────

public class SamplingPolicyViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ProjectId { get; set; }
    public string? CallTypeFilter { get; set; }
    public int? MinDurationSeconds { get; set; }
    public int? MaxDurationSeconds { get; set; }
    public string SamplingMethod { get; set; } = "Percentage";
    public float SampleValue { get; set; } = 10f;
    public bool IsActive { get; set; } = true;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateSamplingPolicyViewModel
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string? Description { get; set; }

    public int? ProjectId { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string? CallTypeFilter { get; set; }

    public int? MinDurationSeconds { get; set; }
    public int? MaxDurationSeconds { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    public string SamplingMethod { get; set; } = "Percentage";

    public float SampleValue { get; set; } = 10f;

    public bool IsActive { get; set; } = true;
}

public class HumanReviewItemViewModel
{
    public int Id { get; set; }
    public int EvaluationResultId { get; set; }
    public int? SamplingPolicyId { get; set; }
    public string? SamplingPolicyName { get; set; }
    public DateTime SampledAt { get; set; }
    public string SampledBy { get; set; } = string.Empty;
    public string? AssignedTo { get; set; }
    public string Status { get; set; } = "Pending";
    public string? ReviewerComment { get; set; }
    public string? ReviewVerdict { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    // Embedded AI audit info
    public string? AgentName { get; set; }
    public string? CallReference { get; set; }
    public DateTime? CallDate { get; set; }
    public string? FormName { get; set; }
    public double? AiScorePercent { get; set; }
    public string? AiReasoning { get; set; }
    public int? ProjectId { get; set; }
}

public class SubmitReviewViewModel
{
    public int ReviewItemId { get; set; }
    public string? ReviewerComment { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    public string ReviewVerdict { get; set; } = "Agree";
}

// ── Training Need Identification (TNI) ViewModels ───────────────────────────

public class TrainingPlanItemViewModel
{
    public int Id { get; set; }
    public int TrainingPlanId { get; set; }
    public string TargetArea { get; set; } = string.Empty;
    public string ItemType { get; set; } = "Observation";
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int Order { get; set; }
    public string? CompletedBy { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletionNotes { get; set; }
}

public class TrainingPlanViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string? AgentUsername { get; set; }
    public string TrainerName { get; set; } = string.Empty;
    public string? TrainerUsername { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime? DueDate { get; set; }
    public int? ProjectId { get; set; }
    public int? EvaluationResultId { get; set; }
    public int? HumanReviewItemId { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? ClosedBy { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ClosingNotes { get; set; }
    public List<TrainingPlanItemViewModel> Items { get; set; } = new();
    public int TotalItems { get; set; }
    public int CompletedItems { get; set; }
}

public class CreateTrainingPlanItemViewModel
{
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string TargetArea { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    public string ItemType { get; set; } = "Observation";

    [System.ComponentModel.DataAnnotations.Required]
    public string Content { get; set; } = string.Empty;

    public int Order { get; set; }
}

public class CreateTrainingPlanViewModel
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string AgentName { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string? AgentUsername { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string TrainerName { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string? TrainerUsername { get; set; }

    public DateTime? DueDate { get; set; }

    public int? ProjectId { get; set; }

    /// <summary>Pre-filled from audit context when creating from an audit/review page.</summary>
    public int? EvaluationResultId { get; set; }
    public int? HumanReviewItemId { get; set; }

    /// <summary>JSON-encoded list of items submitted from the dynamic form rows.</summary>
    public string ItemsJson { get; set; } = "[]";
}

public class CloseTrainingPlanViewModel
{
    public int PlanId { get; set; }
    public string? ClosingNotes { get; set; }
}

// ── Insights Chat ViewModels ────────────────────────────────────────────────

public class InsightsChatResultViewModel
{
    public string Question { get; set; } = string.Empty;
    public string Sql { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = new();
    public List<List<object?>> Rows { get; set; } = new();
    public string Insights { get; set; } = string.Empty;
    public int RowCount { get; set; }
    public string? Error { get; set; }
}
