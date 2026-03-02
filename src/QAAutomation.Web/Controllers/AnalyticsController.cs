using Microsoft.AspNetCore.Mvc;
using QAAutomation.Web.Services;

namespace QAAutomation.Web.Controllers;

public class AnalyticsController : Controller
{
    private readonly ApiClient _api;

    public AnalyticsController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index()
    {
        var vm = await _api.GetAnalytics();
        return View(vm ?? new Models.AnalyticsViewModel());
    }
}
