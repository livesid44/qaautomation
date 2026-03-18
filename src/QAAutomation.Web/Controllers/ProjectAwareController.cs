using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QAAutomation.Web.Controllers;

/// <summary>Base controller that exposes the currently selected Project from claims.</summary>
[Authorize]
public abstract class ProjectAwareController : Controller
{
    /// <summary>Current project ID from auth claims. 0 if no project selected yet.</summary>
    protected int CurrentProjectId =>
        int.TryParse(User.FindFirst("project_id")?.Value, out var id) ? id : 0;

    protected string CurrentProjectName =>
        User.FindFirst("project_name")?.Value ?? "";

    protected int ProjectCount =>
        int.TryParse(User.FindFirst("project_count")?.Value, out var c) ? c : 1;
}
