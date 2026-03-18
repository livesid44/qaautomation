using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAAutomation.Web.Services;

namespace QAAutomation.Web.Controllers;

/// <summary>
/// Browser-callable diagnostics endpoint.
/// GET /diagnostics/ping  — tests connectivity to the backend API and returns
/// a JSON result that is visible in the browser's Network tab.
/// </summary>
[Authorize]
public class DiagnosticsController : Controller
{
    private readonly ApiClient _api;

    public DiagnosticsController(ApiClient api) => _api = api;

    [HttpGet("diagnostics/ping")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Ping()
    {
        var result = await _api.PingAsync();
        return Json(result);
    }
}
