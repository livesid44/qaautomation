using Microsoft.AspNetCore.Mvc;
using QAAutomation.Web.Models;
using QAAutomation.Web.Services;

namespace QAAutomation.Web.Controllers;

public class DashboardController : ProjectAwareController
{
    private readonly ApiClient _api;

    public DashboardController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index()
    {
        var pid = CurrentProjectId > 0 ? (int?)CurrentProjectId : null;

        var parameters = await _api.GetParameters(pid);
        var clubs = await _api.GetParameterClubs(pid);
        var criteria = await _api.GetRatingCriteria(pid);
        var forms = await _api.GetEvaluationForms(pid);
        var audits = await _api.GetAudits(pid);

        var vm = new DashboardViewModel
        {
            ParameterCount = parameters.Count,
            ParameterClubCount = clubs.Count,
            RatingCriteriaCount = criteria.Count,
            EvaluationFormCount = forms.Count,
            AuditCount = audits.Count,
            Username = User.Identity?.Name ?? "",
            Role = User.IsInRole("Admin") ? "Admin" : "User",
            CurrentProjectId = CurrentProjectId,
            CurrentProjectName = CurrentProjectName,
        };
        return View(vm);
    }
}
