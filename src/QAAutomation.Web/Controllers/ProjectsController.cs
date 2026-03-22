using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAAutomation.Web.Models;
using QAAutomation.Web.Services;

namespace QAAutomation.Web.Controllers;

[Authorize(Roles = "Admin")]
public class ProjectsController : Controller
{
    private readonly ApiClient _api;
    public ProjectsController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index()
    {
        var projects = await _api.GetProjects();
        ViewData["Title"] = "Projects";
        return View(projects);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "New Project";
        return View(new ProjectViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string? description)
    {
        var result = await _api.CreateProject(new { name, description });
        if (result == null) { TempData["Error"] = "Failed to create project."; return View(); }
        TempData["Success"] = $"Project '{name}' created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var project = await _api.GetProject(id);
        if (project == null) return NotFound();
        ViewData["Title"] = $"Edit — {project.Name}";
        var users = await _api.GetProjectUsers(id);
        var allUsers = await _api.GetUsers();
        ViewData["ProjectUsers"] = users;
        ViewData["AllUsers"] = allUsers;
        return View(project);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string name, string? description, bool isActive,
        bool piiProtectionEnabled = false, string piiRedactionMode = "Redact")
    {
        await _api.UpdateProject(id, new { name, description, isActive, piiProtectionEnabled, piiRedactionMode });
        TempData["Success"] = "Project saved.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GrantAccess(int id, int userId)
    {
        await _api.GrantProjectAccess(id, userId);
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeAccess(int id, int userId)
    {
        await _api.RevokeProjectAccess(id, userId);
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _api.DeleteProject(id);
        TempData["Success"] = "Project deleted.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Returns the projects list as JSON for the project switcher widget.</summary>
    [HttpGet]
    public async Task<IActionResult> GetProjectsJson()
    {
        var projects = await _api.GetProjects();
        return Json(projects);
    }
}
