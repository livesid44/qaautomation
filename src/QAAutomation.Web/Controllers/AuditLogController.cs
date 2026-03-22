using Microsoft.AspNetCore.Mvc;
using QAAutomation.Web.Models;
using QAAutomation.Web.Services;

namespace QAAutomation.Web.Controllers;

/// <summary>
/// Displays the tenant-scoped audit log covering PII/SPII protection events
/// and all outbound external API calls.
/// </summary>
public class AuditLogController : ProjectAwareController
{
    private readonly ApiClient _api;

    public AuditLogController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index(
        string? category = null,
        string? eventType = null,
        string? outcome = null,
        string? from = null,
        string? to = null,
        int page = 1,
        int pageSize = 50)
    {
        var pid = CurrentProjectId > 0 ? (int?)CurrentProjectId : null;

        var result = await _api.GetAuditLogs(pid, category, eventType, outcome, from, to, page, pageSize);

        var vm = result ?? new AuditLogPageViewModel();
        vm.FilterCategory  = category;
        vm.FilterEventType = eventType;
        vm.FilterOutcome   = outcome;
        vm.FilterFrom      = from;
        vm.FilterTo        = to;

        return View(vm);
    }
}
