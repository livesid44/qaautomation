using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using QAAutomation.Web.Models;
using QAAutomation.Web.Services;
using System.Security.Claims;

namespace QAAutomation.Web.Controllers;

public class AccountController : Controller
{
    private readonly ApiClient _api;

    public AccountController(ApiClient api) => _api = api;

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Analytics");
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);

        var (success, role, message, projects) = await _api.Login(model.Username, model.Password);
        if (!success)
        {
            ModelState.AddModelError("", message.Length > 0 ? message : "Invalid username or password.");
            return View(model);
        }

        // Sign in with auth cookie — project will be set below
        var projectsJson = System.Text.Json.JsonSerializer.Serialize(
            projects.Select(p => new { id = p.Id, name = p.Name }));
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, model.Username),
            new(ClaimTypes.Role, role),
            new("project_count", projects.Count.ToString()),
            new("projects_list", projectsJson)
        };

        // If only one project, auto-select it
        if (projects.Count == 1)
        {
            claims.Add(new Claim("project_id", projects[0].Id.ToString()));
            claims.Add(new Claim("project_name", projects[0].Name));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = true });

        // Multiple projects → choose project first
        if (projects.Count > 1)
        {
            HttpContext.Session.SetString("pending_projects", System.Text.Json.JsonSerializer.Serialize(projects));
            return RedirectToAction("SelectProject");
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Analytics");
    }

    [HttpGet]
    public IActionResult SelectProject()
    {
        if (User.Identity?.IsAuthenticated != true) return RedirectToAction("Login");

        // Get projects from session (set during login) or from claims
        var json = HttpContext.Session.GetString("pending_projects");
        if (string.IsNullOrEmpty(json))
            return RedirectToAction("Index", "Analytics");

        var projects = System.Text.Json.JsonSerializer.Deserialize<List<ProjectViewModel>>(json) ?? new();
        return View(new SelectProjectViewModel
        {
            Projects = projects,
            Username = User.Identity.Name ?? ""
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectProject(int projectId, string projectName)
    {
        if (User.Identity?.IsAuthenticated != true) return RedirectToAction("Login");

        HttpContext.Session.Remove("pending_projects");
        await SetProjectAsync(projectId, projectName);
        return RedirectToAction("Index", "Analytics");
    }

    /// <summary>Switch the active project for the current session.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SwitchProject(int projectId, string projectName, string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated != true) return RedirectToAction("Login");
        await SetProjectAsync(projectId, projectName);
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Index", "Analytics");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SetLanguage(string culture, string returnUrl)
    {
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax
            });
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);
        return RedirectToAction("Index", "Analytics");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task SetProjectAsync(int projectId, string projectName)
    {
        // Re-issue the auth cookie with updated project claims, preserving all other claims
        var claims = User.Claims
            .Where(c => c.Type != "project_id" && c.Type != "project_name")
            .ToList();
        claims.Add(new Claim("project_id", projectId.ToString()));
        claims.Add(new Claim("project_name", projectName));

        // Preserve projects_list if already present; only fetch when missing (e.g. old session)
        if (!claims.Any(c => c.Type == "projects_list"))
        {
            var allProjects = await _api.GetProjects();
            var json = System.Text.Json.JsonSerializer.Serialize(allProjects.Select(p => new { id = p.Id, name = p.Name }));
            claims.Add(new Claim("projects_list", json));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }
}
