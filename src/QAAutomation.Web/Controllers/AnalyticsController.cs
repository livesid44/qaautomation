using Microsoft.AspNetCore.Mvc;
using QAAutomation.Web.Services;

namespace QAAutomation.Web.Controllers;

public class AnalyticsController : ProjectAwareController
{
    private readonly ApiClient _api;

    public AnalyticsController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index()
    {
        var pid = CurrentProjectId > 0 ? (int?)CurrentProjectId : null;
        var vm = await _api.GetAnalytics(pid);
        return View(vm ?? new Models.AnalyticsViewModel());
    }

    public async Task<IActionResult> Explainability()
    {
        var pid = CurrentProjectId > 0 ? (int?)CurrentProjectId : null;
        var vm = await _api.GetExplainabilityAnalytics(pid);
        return View(vm ?? new Models.ExplainabilityViewModel());
    }
}
