using Microsoft.AspNetCore.Mvc;
using QAAutomation.Web.Services;

namespace QAAutomation.Web.Controllers;

public class InsightsChatController : ProjectAwareController
{
    private readonly ApiClient _api;

    public InsightsChatController(ApiClient api) => _api = api;

    public IActionResult Index()
    {
        ViewData["Title"] = "Insights Chat";
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Ask([FromBody] AskRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Question))
            return BadRequest(new { error = "Question is required." });

        // Always scope to the current tenant project
        var projectId = CurrentProjectId > 0 ? (int?)CurrentProjectId : req.ProjectId;
        var result = await _api.InsightsChat(req.Question, projectId);

        // result is never null — errors are now reported via result.Error
        return Json(result);
    }

    public record AskRequest(string Question, int? ProjectId);
}
