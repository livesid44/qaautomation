using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAAutomation.Web.Models;
using QAAutomation.Web.Services;

namespace QAAutomation.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ApiClient _api;

    public DashboardController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index()
    {
        var parameters = await _api.GetParameters();
        var clubs = await _api.GetParameterClubs();
        var criteria = await _api.GetRatingCriteria();
        var forms = await _api.GetEvaluationForms();
        var audits = await _api.GetAudits();

        var vm = new DashboardViewModel
        {
            ParameterCount = parameters.Count,
            ParameterClubCount = clubs.Count,
            RatingCriteriaCount = criteria.Count,
            EvaluationFormCount = forms.Count,
            AuditCount = audits.Count,
            Username = User.Identity?.Name ?? "",
            Role = User.IsInRole("Admin") ? "Admin" : "User"
        };
        return View(vm);
    }
}
