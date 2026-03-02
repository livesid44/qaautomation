using System.Net.Http.Json;
using System.Text.Json;
using QAAutomation.Web.Models;

namespace QAAutomation.Web.Services;

public class ApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<ApiClient> _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ApiClient(HttpClient http, ILogger<ApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    // Auth - returns projects in addition to role/success
    public async Task<(bool success, string role, string message, List<ProjectViewModel> projects)> Login(string username, string password)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/auth/login", new { username, password });
            if (!resp.IsSuccessStatusCode) return (false, "", "Server error", new());
            var result = await resp.Content.ReadFromJsonAsync<JsonElement>();
            bool success = result.GetProperty("success").GetBoolean();
            string role = success ? result.GetProperty("role").GetString() ?? "" : "";
            string message = result.GetProperty("message").GetString() ?? "";
            var projects = new List<ProjectViewModel>();
            if (success && result.TryGetProperty("projects", out var projProp))
            {
                projects = JsonSerializer.Deserialize<List<ProjectViewModel>>(projProp.GetRawText(), _jsonOptions) ?? new();
            }
            return (success, role, message, projects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed");
            return (false, "", "Connection error", new());
        }
    }

    // Parameters — pass projectId when known
    public async Task<List<ParameterViewModel>> GetParameters(int? projectId = null)
    {
        var url = projectId.HasValue ? $"api/parameters?projectId={projectId}" : "api/parameters";
        try { return await _http.GetFromJsonAsync<List<ParameterViewModel>>(url, _jsonOptions) ?? new(); }
        catch (Exception ex) { _logger.LogError(ex, "GetParameters failed"); return new(); }
    }

    public async Task<ParameterViewModel?> GetParameter(int id)
    {
        try { return await _http.GetFromJsonAsync<ParameterViewModel>($"api/parameters/{id}", _jsonOptions); }
        catch (Exception ex) { _logger.LogError(ex, "GetParameter {Id} failed", id); return null; }
    }

    public async Task<bool> CreateParameter(object dto)
    {
        try { var r = await _http.PostAsJsonAsync("api/parameters", dto); return r.IsSuccessStatusCode; }
        catch (Exception ex) { _logger.LogError(ex, "CreateParameter failed"); return false; }
    }

    public async Task<bool> UpdateParameter(int id, object dto)
    {
        try { var r = await _http.PutAsJsonAsync($"api/parameters/{id}", dto); return r.IsSuccessStatusCode; }
        catch (Exception ex) { _logger.LogError(ex, "UpdateParameter failed"); return false; }
    }

    public async Task<bool> DeleteParameter(int id)
    {
        try { var r = await _http.DeleteAsync($"api/parameters/{id}"); return r.IsSuccessStatusCode; }
        catch (Exception ex) { _logger.LogError(ex, "DeleteParameter failed"); return false; }
    }

    // Parameter Clubs
    public async Task<List<ParameterClubViewModel>> GetParameterClubs(int? projectId = null)
    {
        var url = projectId.HasValue ? $"api/parameterclubs?projectId={projectId}" : "api/parameterclubs";
        try { return await _http.GetFromJsonAsync<List<ParameterClubViewModel>>(url, _jsonOptions) ?? new(); }
        catch (Exception ex) { _logger.LogError(ex, "GetParameterClubs failed"); return new(); }
    }

    public async Task<ParameterClubViewModel?> GetParameterClub(int id)
    {
        try { return await _http.GetFromJsonAsync<ParameterClubViewModel>($"api/parameterclubs/{id}", _jsonOptions); }
        catch (Exception ex) { _logger.LogError(ex, "GetParameterClub {Id} failed", id); return null; }
    }

    public async Task<ParameterClubViewModel?> CreateParameterClub(object dto)
    {
        try
        {
            var r = await _http.PostAsJsonAsync("api/parameterclubs", dto);
            if (!r.IsSuccessStatusCode) return null;
            return await r.Content.ReadFromJsonAsync<ParameterClubViewModel>(_jsonOptions);
        }
        catch (Exception ex) { _logger.LogError(ex, "CreateParameterClub failed"); return null; }
    }

    public async Task<bool> UpdateParameterClub(int id, object dto)
    {
        try { var r = await _http.PutAsJsonAsync($"api/parameterclubs/{id}", dto); return r.IsSuccessStatusCode; }
        catch (Exception ex) { _logger.LogError(ex, "UpdateParameterClub failed"); return false; }
    }

    public async Task<bool> UpdateClubItems(int id, object items)
    {
        try { var r = await _http.PutAsJsonAsync($"api/parameterclubs/{id}/items", items); return r.IsSuccessStatusCode; }
        catch (Exception ex) { _logger.LogError(ex, "UpdateClubItems failed"); return false; }
    }

    public async Task<bool> DeleteParameterClub(int id)
    {
        try { var r = await _http.DeleteAsync($"api/parameterclubs/{id}"); return r.IsSuccessStatusCode; }
        catch (Exception ex) { _logger.LogError(ex, "DeleteParameterClub failed"); return false; }
    }

    // Rating Criteria
    public async Task<List<RatingCriteriaViewModel>> GetRatingCriteria(int? projectId = null)
    {
        var url = projectId.HasValue ? $"api/ratingcriteria?projectId={projectId}" : "api/ratingcriteria";
        try { return await _http.GetFromJsonAsync<List<RatingCriteriaViewModel>>(url, _jsonOptions) ?? new(); }
        catch (Exception ex) { _logger.LogError(ex, "GetRatingCriteria failed"); return new(); }
    }

    public async Task<RatingCriteriaViewModel?> GetRatingCriteriaById(int id)
    {
        try { return await _http.GetFromJsonAsync<RatingCriteriaViewModel>($"api/ratingcriteria/{id}", _jsonOptions); }
        catch (Exception ex) { _logger.LogError(ex, "GetRatingCriteria {Id} failed", id); return null; }
    }

    public async Task<RatingCriteriaViewModel?> CreateRatingCriteria(object dto)
    {
        try
        {
            var r = await _http.PostAsJsonAsync("api/ratingcriteria", dto);
            if (!r.IsSuccessStatusCode) return null;
            return await r.Content.ReadFromJsonAsync<RatingCriteriaViewModel>(_jsonOptions);
        }
        catch (Exception ex) { _logger.LogError(ex, "CreateRatingCriteria failed"); return null; }
    }

    public async Task<bool> UpdateRatingCriteria(int id, object dto)
    {
        try { var r = await _http.PutAsJsonAsync($"api/ratingcriteria/{id}", dto); return r.IsSuccessStatusCode; }
        catch (Exception ex) { _logger.LogError(ex, "UpdateRatingCriteria failed"); return false; }
    }

    public async Task<bool> DeleteRatingCriteria(int id)
    {
        try { var r = await _http.DeleteAsync($"api/ratingcriteria/{id}"); return r.IsSuccessStatusCode; }
        catch (Exception ex) { _logger.LogError(ex, "DeleteRatingCriteria failed"); return false; }
    }

    // Users
    public async Task<List<UserViewModel>> GetUsers()
    {
        try { return await _http.GetFromJsonAsync<List<UserViewModel>>("api/users", _jsonOptions) ?? new(); }
        catch (Exception ex) { _logger.LogError(ex, "GetUsers failed"); return new(); }
    }

    public async Task<UserViewModel?> GetUser(int id)
    {
        try { return await _http.GetFromJsonAsync<UserViewModel>($"api/users/{id}", _jsonOptions); }
        catch (Exception ex) { _logger.LogError(ex, "GetUser {Id} failed", id); return null; }
    }

    public async Task<bool> CreateUser(object dto)
    {
        try { var r = await _http.PostAsJsonAsync("api/users", dto); return r.IsSuccessStatusCode; }
        catch (Exception ex) { _logger.LogError(ex, "CreateUser failed"); return false; }
    }

    public async Task<bool> UpdateUser(int id, object dto)
    {
        try { var r = await _http.PutAsJsonAsync($"api/users/{id}", dto); return r.IsSuccessStatusCode; }
        catch (Exception ex) { _logger.LogError(ex, "UpdateUser failed"); return false; }
    }

    public async Task<bool> DeleteUser(int id)
    {
        try { var r = await _http.DeleteAsync($"api/users/{id}"); return r.IsSuccessStatusCode; }
        catch (Exception ex) { _logger.LogError(ex, "DeleteUser failed"); return false; }
    }

    // Evaluation Forms
    public async Task<List<EvaluationFormViewModel>> GetEvaluationForms(int? projectId = null)
    {
        var url = projectId.HasValue ? $"api/evaluationforms?projectId={projectId}" : "api/evaluationforms";
        try { return await _http.GetFromJsonAsync<List<EvaluationFormViewModel>>(url, _jsonOptions) ?? new(); }
        catch (Exception ex) { _logger.LogError(ex, "GetEvaluationForms failed"); return new(); }
    }

    public async Task<EvaluationFormViewModel?> GetEvaluationForm(int id)
    {
        try { return await _http.GetFromJsonAsync<EvaluationFormViewModel>($"api/evaluationforms/{id}", _jsonOptions); }
        catch (Exception ex) { _logger.LogError(ex, "GetEvaluationForm {Id} failed", id); return null; }
    }

    public async Task<bool> SaveEvaluationForm(object dto)
    {
        try { var r = await _http.PostAsJsonAsync("api/evaluationforms", dto); return r.IsSuccessStatusCode; }
        catch (Exception ex) { _logger.LogError(ex, "SaveEvaluationForm failed"); return false; }
    }

    public async Task<bool> DeleteEvaluationForm(int id)
    {
        try { var r = await _http.DeleteAsync($"api/evaluationforms/{id}"); return r.IsSuccessStatusCode; }
        catch (Exception ex) { _logger.LogError(ex, "DeleteEvaluationForm failed"); return false; }
    }

    // Audits (EvaluationResults)
    public async Task<List<AuditViewModel>> GetAudits(int? projectId = null)
    {
        var url = projectId.HasValue ? $"api/evaluationresults?projectId={projectId.Value}" : "api/evaluationresults";
        try { return await _http.GetFromJsonAsync<List<AuditViewModel>>(url, _jsonOptions) ?? new(); }
        catch (Exception ex) { _logger.LogError(ex, "GetAudits failed"); return new(); }
    }

    public async Task<AuditViewModel?> GetAudit(int id)
    {
        try { return await _http.GetFromJsonAsync<AuditViewModel>($"api/evaluationresults/{id}", _jsonOptions); }
        catch (Exception ex) { _logger.LogError(ex, "GetAudit failed"); return null; }
    }

    public async Task<List<AuditViewModel>> GetAuditsByForm(int formId)
    {
        try { return await _http.GetFromJsonAsync<List<AuditViewModel>>($"api/evaluationresults/byform/{formId}", _jsonOptions) ?? new(); }
        catch (Exception ex) { _logger.LogError(ex, "GetAuditsByForm failed"); return new(); }
    }

    public async Task<bool> CreateAudit(object dto)
    {
        try { var r = await _http.PostAsJsonAsync("api/evaluationresults", dto); return r.IsSuccessStatusCode; }
        catch (Exception ex) { _logger.LogError(ex, "CreateAudit failed"); return false; }
    }

    public async Task<bool> DeleteAudit(int id)
    {
        try { var r = await _http.DeleteAsync($"api/evaluationresults/{id}"); return r.IsSuccessStatusCode; }
        catch (Exception ex) { _logger.LogError(ex, "DeleteAudit failed"); return false; }
    }

    // Legacy forms (EvaluationForms with sections and FormFields)
    public async Task<List<LegacyFormViewModel>> GetLegacyForms()
    {
        try { return await _http.GetFromJsonAsync<List<LegacyFormViewModel>>("api/evaluationforms", _jsonOptions) ?? new(); }
        catch (Exception ex) { _logger.LogError(ex, "GetLegacyForms failed"); return new(); }
    }

    public async Task<LegacyFormViewModel?> GetLegacyForm(int id)
    {
        try { return await _http.GetFromJsonAsync<LegacyFormViewModel>($"api/evaluationforms/{id}", _jsonOptions); }
        catch (Exception ex) { _logger.LogError(ex, "GetLegacyForm failed"); return null; }
    }

    // Auto Audit (LLM transcript analysis)
    public async Task<AutoAuditReviewViewModel?> AutoAnalyze(object request)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/autoaudit/analyze", request);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("AutoAnalyze failed: {Status}", resp.StatusCode);
                return null;
            }
            return await resp.Content.ReadFromJsonAsync<AutoAuditReviewViewModel>(_jsonOptions);
        }
        catch (Exception ex) { _logger.LogError(ex, "AutoAnalyze failed"); return null; }
    }

    // Sentiment & Emotion analysis
    public async Task<SentimentViewModel?> AnalyzeSentiment(object request)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/sentiment/analyze", request);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("AnalyzeSentiment failed: {Status}", resp.StatusCode);
                return null;
            }
            return await resp.Content.ReadFromJsonAsync<SentimentViewModel>(_jsonOptions);
        }
        catch (Exception ex) { _logger.LogError(ex, "AnalyzeSentiment failed"); return null; }
    }

    // AI Settings (RAG configuration stored in DB)
    public async Task<AiSettingsViewModel?> GetAiSettings()
    {
        try { return await _http.GetFromJsonAsync<AiSettingsViewModel>("api/aiconfig", _jsonOptions); }
        catch (Exception ex) { _logger.LogError(ex, "GetAiSettings failed"); return null; }
    }

    public async Task<bool> SaveAiSettings(AiSettingsViewModel model)
    {
        try
        {
            var resp = await _http.PutAsJsonAsync("api/aiconfig", model);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                _logger.LogWarning("SaveAiSettings API returned {Status}: {Body}", (int)resp.StatusCode, body);
            }
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex) { _logger.LogError(ex, "SaveAiSettings failed"); return false; }
    }

    // Knowledge Base sources
    public async Task<List<KnowledgeSourceViewModel>> GetKnowledgeSources(int? projectId = null)
    {
        var url = projectId.HasValue ? $"api/knowledgebase/sources?projectId={projectId.Value}" : "api/knowledgebase/sources";
        try { return await _http.GetFromJsonAsync<List<KnowledgeSourceViewModel>>(url, _jsonOptions) ?? new(); }
        catch (Exception ex) { _logger.LogError(ex, "GetKnowledgeSources failed"); return new(); }
    }

    public async Task<KnowledgeSourceViewModel?> GetKnowledgeSource(int id)
    {
        try { return await _http.GetFromJsonAsync<KnowledgeSourceViewModel>($"api/knowledgebase/sources/{id}", _jsonOptions); }
        catch (Exception ex) { _logger.LogError(ex, "GetKnowledgeSource failed"); return null; }
    }

    public async Task<KnowledgeSourceViewModel?> CreateKnowledgeSource(KnowledgeSourceViewModel model)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/knowledgebase/sources", model);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<KnowledgeSourceViewModel>(_jsonOptions);
        }
        catch (Exception ex) { _logger.LogError(ex, "CreateKnowledgeSource failed"); return null; }
    }

    public async Task<bool> UpdateKnowledgeSource(int id, KnowledgeSourceViewModel model)
    {
        try { var resp = await _http.PutAsJsonAsync($"api/knowledgebase/sources/{id}", model); return resp.IsSuccessStatusCode; }
        catch (Exception ex) { _logger.LogError(ex, "UpdateKnowledgeSource failed"); return false; }
    }

    public async Task<bool> DeleteKnowledgeSource(int id)
    {
        try { var resp = await _http.DeleteAsync($"api/knowledgebase/sources/{id}"); return resp.IsSuccessStatusCode; }
        catch (Exception ex) { _logger.LogError(ex, "DeleteKnowledgeSource failed"); return false; }
    }

    // Knowledge Base documents
    public async Task<List<KnowledgeDocumentViewModel>> GetKnowledgeDocuments(int? sourceId = null)
    {
        var url = sourceId.HasValue ? $"api/knowledgebase/documents?sourceId={sourceId}" : "api/knowledgebase/documents";
        try { return await _http.GetFromJsonAsync<List<KnowledgeDocumentViewModel>>(url, _jsonOptions) ?? new(); }
        catch (Exception ex) { _logger.LogError(ex, "GetKnowledgeDocuments failed"); return new(); }
    }

    public async Task<bool> AddKnowledgeDocument(object dto)
    {
        try { var resp = await _http.PostAsJsonAsync("api/knowledgebase/documents", dto); return resp.IsSuccessStatusCode; }
        catch (Exception ex) { _logger.LogError(ex, "AddKnowledgeDocument failed"); return false; }
    }

    public async Task<bool> DeleteKnowledgeDocument(int id)
    {
        try { var resp = await _http.DeleteAsync($"api/knowledgebase/documents/{id}"); return resp.IsSuccessStatusCode; }
        catch (Exception ex) { _logger.LogError(ex, "DeleteKnowledgeDocument failed"); return false; }
    }

    // ── Projects ──────────────────────────────────────────────────────────────
    public async Task<List<ProjectViewModel>> GetProjects()
    {
        try { return await _http.GetFromJsonAsync<List<ProjectViewModel>>("api/projects", _jsonOptions) ?? new(); }
        catch (Exception ex) { _logger.LogError(ex, "GetProjects failed"); return new(); }
    }

    public async Task<ProjectViewModel?> GetProject(int id)
    {
        try { return await _http.GetFromJsonAsync<ProjectViewModel>($"api/projects/{id}", _jsonOptions); }
        catch (Exception ex) { _logger.LogError(ex, "GetProject failed"); return null; }
    }

    public async Task<ProjectViewModel?> CreateProject(object dto)
    {
        try
        {
            var r = await _http.PostAsJsonAsync("api/projects", dto);
            if (!r.IsSuccessStatusCode) return null;
            return await r.Content.ReadFromJsonAsync<ProjectViewModel>(_jsonOptions);
        }
        catch (Exception ex) { _logger.LogError(ex, "CreateProject failed"); return null; }
    }

    public async Task<bool> UpdateProject(int id, object dto)
    {
        try { var r = await _http.PutAsJsonAsync($"api/projects/{id}", dto); return r.IsSuccessStatusCode; }
        catch (Exception ex) { _logger.LogError(ex, "UpdateProject failed"); return false; }
    }

    public async Task<bool> DeleteProject(int id)
    {
        try { var r = await _http.DeleteAsync($"api/projects/{id}"); return r.IsSuccessStatusCode; }
        catch (Exception ex) { _logger.LogError(ex, "DeleteProject failed"); return false; }
    }

    public async Task<List<ProjectUserViewModel>> GetProjectUsers(int projectId)
    {
        try { return await _http.GetFromJsonAsync<List<ProjectUserViewModel>>($"api/projects/{projectId}/users", _jsonOptions) ?? new(); }
        catch (Exception ex) { _logger.LogError(ex, "GetProjectUsers failed"); return new(); }
    }

    public async Task<bool> GrantProjectAccess(int projectId, int userId)
    {
        try { var r = await _http.PostAsync($"api/projects/{projectId}/users/{userId}", null); return r.IsSuccessStatusCode; }
        catch (Exception ex) { _logger.LogError(ex, "GrantProjectAccess failed"); return false; }
    }

    public async Task<bool> RevokeProjectAccess(int projectId, int userId)
    {
        try { var r = await _http.DeleteAsync($"api/projects/{projectId}/users/{userId}"); return r.IsSuccessStatusCode; }
        catch (Exception ex) { _logger.LogError(ex, "RevokeProjectAccess failed"); return false; }
    }

    // ── LOBs ──────────────────────────────────────────────────────────────────
    public async Task<List<LobViewModel>> GetLobs(int? projectId = null)
    {
        var url = projectId.HasValue ? $"api/lobs?projectId={projectId}" : "api/lobs";
        try { return await _http.GetFromJsonAsync<List<LobViewModel>>(url, _jsonOptions) ?? new(); }
        catch (Exception ex) { _logger.LogError(ex, "GetLobs failed"); return new(); }
    }

    public async Task<LobViewModel?> GetLob(int id)
    {
        try { return await _http.GetFromJsonAsync<LobViewModel>($"api/lobs/{id}", _jsonOptions); }
        catch (Exception ex) { _logger.LogError(ex, "GetLob failed"); return null; }
    }

    public async Task<LobViewModel?> CreateLob(object dto)
    {
        try
        {
            var r = await _http.PostAsJsonAsync("api/lobs", dto);
            if (!r.IsSuccessStatusCode) return null;
            return await r.Content.ReadFromJsonAsync<LobViewModel>(_jsonOptions);
        }
        catch (Exception ex) { _logger.LogError(ex, "CreateLob failed"); return null; }
    }

    public async Task<bool> UpdateLob(int id, object dto)
    {
        try { var r = await _http.PutAsJsonAsync($"api/lobs/{id}", dto); return r.IsSuccessStatusCode; }
        catch (Exception ex) { _logger.LogError(ex, "UpdateLob failed"); return false; }
    }

    public async Task<bool> DeleteLob(int id)
    {
        try { var r = await _http.DeleteAsync($"api/lobs/{id}"); return r.IsSuccessStatusCode; }
        catch (Exception ex) { _logger.LogError(ex, "DeleteLob failed"); return false; }
    }

    // Analytics
    public async Task<AnalyticsViewModel?> GetAnalytics(int? projectId = null)
    {
        var url = projectId.HasValue ? $"api/analytics?projectId={projectId.Value}" : "api/analytics";
        try { return await _http.GetFromJsonAsync<AnalyticsViewModel>(url, _jsonOptions); }
        catch (Exception ex) { _logger.LogError(ex, "GetAnalytics failed"); return null; }
    }

    // ── Call Pipeline ─────────────────────────────────────────────────────────

    public async Task<List<CallPipelineJobViewModel>> GetPipelineJobs(int? projectId = null)
    {
        var url = projectId.HasValue ? $"api/callpipeline?projectId={projectId.Value}" : "api/callpipeline";
        try { return await _http.GetFromJsonAsync<List<CallPipelineJobViewModel>>(url, _jsonOptions) ?? new(); }
        catch (Exception ex) { _logger.LogError(ex, "GetPipelineJobs failed"); return new(); }
    }

    public async Task<CallPipelineJobViewModel?> GetPipelineJob(int id)
    {
        try { return await _http.GetFromJsonAsync<CallPipelineJobViewModel>($"api/callpipeline/{id}", _jsonOptions); }
        catch (Exception ex) { _logger.LogError(ex, "GetPipelineJob failed"); return null; }
    }

    public async Task<CallPipelineJobViewModel?> CreateBatchUrlPipelineJob(object dto)
    {
        try
        {
            var r = await _http.PostAsJsonAsync("api/callpipeline/batch-urls", dto);
            if (!r.IsSuccessStatusCode) return null;
            return await r.Content.ReadFromJsonAsync<CallPipelineJobViewModel>(_jsonOptions);
        }
        catch (Exception ex) { _logger.LogError(ex, "CreateBatchUrlPipelineJob failed"); return null; }
    }

    public async Task<CallPipelineJobViewModel?> CreateConnectorPipelineJob(object dto)
    {
        try
        {
            var r = await _http.PostAsJsonAsync("api/callpipeline/from-connector", dto);
            if (!r.IsSuccessStatusCode) return null;
            return await r.Content.ReadFromJsonAsync<CallPipelineJobViewModel>(_jsonOptions);
        }
        catch (Exception ex) { _logger.LogError(ex, "CreateConnectorPipelineJob failed"); return null; }
    }

    public async Task<CallPipelineJobViewModel?> TriggerPipelineProcess(int jobId)
    {
        try
        {
            var r = await _http.PostAsync($"api/callpipeline/{jobId}/process", null);
            if (!r.IsSuccessStatusCode) return null;
            return await r.Content.ReadFromJsonAsync<CallPipelineJobViewModel>(_jsonOptions);
        }
        catch (Exception ex) { _logger.LogError(ex, "TriggerPipelineProcess failed"); return null; }
    }
}
