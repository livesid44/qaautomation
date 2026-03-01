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

    // Auth
    public async Task<(bool success, string role, string message)> Login(string username, string password)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/auth/login", new { username, password });
            if (!resp.IsSuccessStatusCode) return (false, "", "Server error");
            var result = await resp.Content.ReadFromJsonAsync<JsonElement>();
            bool success = result.GetProperty("success").GetBoolean();
            string role = success ? result.GetProperty("role").GetString() ?? "" : "";
            string message = result.GetProperty("message").GetString() ?? "";
            return (success, role, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed");
            return (false, "", "Connection error");
        }
    }

    // Parameters
    public async Task<List<ParameterViewModel>> GetParameters()
    {
        try { return await _http.GetFromJsonAsync<List<ParameterViewModel>>("api/parameters", _jsonOptions) ?? new(); }
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
    public async Task<List<ParameterClubViewModel>> GetParameterClubs()
    {
        try { return await _http.GetFromJsonAsync<List<ParameterClubViewModel>>("api/parameterclubs", _jsonOptions) ?? new(); }
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
    public async Task<List<RatingCriteriaViewModel>> GetRatingCriteria()
    {
        try { return await _http.GetFromJsonAsync<List<RatingCriteriaViewModel>>("api/ratingcriteria", _jsonOptions) ?? new(); }
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
    public async Task<List<EvaluationFormViewModel>> GetEvaluationForms()
    {
        try { return await _http.GetFromJsonAsync<List<EvaluationFormViewModel>>("api/evaluationforms", _jsonOptions) ?? new(); }
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
    public async Task<List<AuditViewModel>> GetAudits()
    {
        try { return await _http.GetFromJsonAsync<List<AuditViewModel>>("api/evaluationresults", _jsonOptions) ?? new(); }
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
}
