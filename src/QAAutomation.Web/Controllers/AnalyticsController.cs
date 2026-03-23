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
        var dataTask     = _api.GetAnalytics(pid);
        var insightsTask = _api.GetAnalyticsInsights(pid);
        await Task.WhenAll(dataTask, insightsTask);
        ViewBag.Insights = await insightsTask ?? new Models.AnalyticsInsightsViewModel();
        return View(await dataTask ?? new Models.AnalyticsViewModel());
    }

    public async Task<IActionResult> Explainability()
    {
        var pid = CurrentProjectId > 0 ? (int?)CurrentProjectId : null;
        var dataTask     = _api.GetExplainabilityAnalytics(pid);
        var insightsTask = _api.GetExplainabilityInsights(pid);
        await Task.WhenAll(dataTask, insightsTask);
        ViewBag.Insights = await insightsTask ?? new Models.ExplainabilityInsightsViewModel();
        return View(await dataTask ?? new Models.ExplainabilityViewModel());
    }

    public async Task<IActionResult> DecisionAssurance()
    {
        var pid = CurrentProjectId > 0 ? (int?)CurrentProjectId : null;
        var model = await _api.GetDecisionAssurance(pid) ?? new Models.DecisionAssuranceViewModel();
        return View(model);
    }
}
