using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAAutomation.Web.Models;
using QAAutomation.Web.Services;

namespace QAAutomation.Web.Controllers;

[Authorize(Roles = "Admin")]
public class LobsController : Controller
{
    private readonly ApiClient _api;
    public LobsController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index(int? projectId = null)
    {
        var lobs = await _api.GetLobs(projectId);
        var projects = await _api.GetProjects();
        ViewData["Title"] = "Lines of Business";
        ViewData["Projects"] = projects;
        ViewData["FilterProjectId"] = projectId;
        return View(lobs);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? projectId = null)
    {
        var projects = await _api.GetProjects();
        ViewData["Projects"] = projects;
        ViewData["DefaultProjectId"] = projectId;
        ViewData["Title"] = "New Line of Business";
        return View(new LobViewModel { ProjectId = projectId ?? 0 });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int projectId, string name, string? description)
    {
        var result = await _api.CreateLob(new { projectId, name, description });
        if (result == null) { TempData["Error"] = "Failed to create LOB."; return RedirectToAction(nameof(Create)); }
        TempData["Success"] = $"LOB '{name}' created.";
        return RedirectToAction(nameof(Index), new { projectId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var lob = await _api.GetLob(id);
        if (lob == null) return NotFound();
        ViewData["Title"] = $"Edit LOB — {lob.Name}";
        return View(lob);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string name, string? description, bool isActive)
    {
        await _api.UpdateLob(id, new { name, description, isActive });
        TempData["Success"] = "LOB saved.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int projectId)
    {
        await _api.DeleteLob(id);
        TempData["Success"] = "LOB deleted.";
        return RedirectToAction(nameof(Index), new { projectId });
    }
}
