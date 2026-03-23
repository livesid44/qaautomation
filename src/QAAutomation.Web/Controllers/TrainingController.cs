using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAAutomation.Web.Models;
using QAAutomation.Web.Services;

namespace QAAutomation.Web.Controllers;

/// <summary>
/// Web UI controller for Training Need Identification (TNI) plans.
///
/// Quality managers (Admin) can:
///   • Create plans from an audit or human review context, or standalone
///   • Edit, activate, and close plans
///
/// Trainers and agents (QA role or any authenticated user) can:
///   • View plans assigned to them
///   • Mark individual plan items as done (trainers only)
///
/// All data is scoped to the currently selected project/tenant.
/// </summary>
[Authorize]
public class TrainingController : ProjectAwareController
{
    private readonly ApiClient _api;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public TrainingController(ApiClient api) => _api = api;

    // ── Index — list plans ────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index(string? status = null)
    {
        var username = User.Identity?.Name;
        var pid = CurrentProjectId > 0 ? (int?)CurrentProjectId : null;
        List<TrainingPlanViewModel> plans;

        if (User.IsInRole("Admin"))
        {
            // Admins / QMs see all plans for the current project (optionally filtered by status)
            plans = await _api.GetTrainingPlans(status: status, projectId: pid);
        }
        else
        {
            // Other roles see plans where they are agent OR trainer, scoped to current project
            var asAgent = await _api.GetTrainingPlans(agentUsername: username, projectId: pid);
            var asTrainer = await _api.GetTrainingPlans(trainerUsername: username, projectId: pid);
            plans = asAgent.Union(asTrainer, new PlanIdComparer()).ToList();
            if (!string.IsNullOrEmpty(status))
                plans = plans.Where(p => p.Status == status).ToList();
        }

        plans = plans.OrderByDescending(p => p.CreatedAt).ToList();
        ViewBag.StatusFilter = status;
        return View(plans);
    }

    // ── Detail ────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var plan = await _api.GetTrainingPlan(id);
        if (plan == null) return NotFound();
        if (!CanAccessPlan(plan)) return Forbid();
        return View(plan);
    }

    // ── Create — from audit context ───────────────────────────────────────────

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public IActionResult Create(int? evaluationResultId = null, int? humanReviewItemId = null,
        string? agentName = null, string? callReference = null, string? formName = null,
        string? aiReasoning = null)
    {
        var vm = new CreateTrainingPlanViewModel
        {
            EvaluationResultId = evaluationResultId,
            HumanReviewItemId = humanReviewItemId,
            AgentName = agentName ?? string.Empty,
            // Pre-populate title if we have context
            Title = agentName != null
                ? $"Training Plan — {agentName}{(callReference != null ? $" ({callReference})" : "")}"
                : string.Empty,
            Description = BuildDefaultDescription(formName, aiReasoning),
            // Scope to the current project automatically
            ProjectId = CurrentProjectId > 0 ? CurrentProjectId : (int?)null
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(CreateTrainingPlanViewModel model)
    {
        // Parse items from JSON (dynamic rows submitted by JS)
        List<CreateTrainingPlanItemViewModel> items;
        try
        {
            items = JsonSerializer.Deserialize<List<CreateTrainingPlanItemViewModel>>(model.ItemsJson, _json) ?? new();
        }
        catch
        {
            items = new();
        }

        if (!ModelState.IsValid || !items.Any())
        {
            if (!items.Any())
                ModelState.AddModelError("ItemsJson", "At least one observation or recommendation is required.");
            return View(model);
        }

        // Always use the server-side project — prevents cross-tenant assignment via form tampering
        var projectId = CurrentProjectId > 0 ? CurrentProjectId : model.ProjectId;

        var dto = new
        {
            title = model.Title,
            description = model.Description,
            agentName = model.AgentName,
            agentUsername = model.AgentUsername,
            trainerName = model.TrainerName,
            trainerUsername = model.TrainerUsername,
            dueDate = model.DueDate,
            projectId,
            evaluationResultId = model.EvaluationResultId,
            humanReviewItemId = model.HumanReviewItemId,
            createdBy = User.Identity?.Name ?? "admin",
            items = items.Select((i, idx) => new
            {
                targetArea = i.TargetArea,
                itemType = i.ItemType,
                content = i.Content,
                order = idx
            }).ToList()
        };

        var created = await _api.CreateTrainingPlan(dto);
        if (created == null)
        {
            ModelState.AddModelError("", "Failed to create training plan. Please try again.");
            return View(model);
        }

        TempData["Success"] = $"Training plan '{created.Title}' created.";
        return RedirectToAction(nameof(Detail), new { id = created.Id });
    }

    // ── Edit ──────────────────────────────────────────────────────────────────

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var plan = await _api.GetTrainingPlan(id);
        if (plan == null) return NotFound();
        if (!CanAccessPlan(plan)) return Forbid();
        if (plan.Status is "Completed" or "Closed")
        {
            TempData["Error"] = "Cannot edit a Completed or Closed training plan.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        var vm = new CreateTrainingPlanViewModel
        {
            EvaluationResultId = plan.EvaluationResultId,
            HumanReviewItemId = plan.HumanReviewItemId,
            Title = plan.Title,
            Description = plan.Description,
            AgentName = plan.AgentName,
            AgentUsername = plan.AgentUsername,
            TrainerName = plan.TrainerName,
            TrainerUsername = plan.TrainerUsername,
            DueDate = plan.DueDate,
            ProjectId = plan.ProjectId,
            ItemsJson = JsonSerializer.Serialize(plan.Items.Select(i => new CreateTrainingPlanItemViewModel
            {
                TargetArea = i.TargetArea,
                ItemType = i.ItemType,
                Content = i.Content,
                Order = i.Order
            }), _json)
        };
        ViewBag.PlanId = id;
        return View("Create", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id, CreateTrainingPlanViewModel model)
    {
        // Validate tenant ownership before updating
        var existing = await _api.GetTrainingPlan(id);
        if (existing == null) return NotFound();
        if (!CanAccessPlan(existing)) return Forbid();

        List<CreateTrainingPlanItemViewModel> items;
        try { items = JsonSerializer.Deserialize<List<CreateTrainingPlanItemViewModel>>(model.ItemsJson, _json) ?? new(); }
        catch { items = new(); }

        if (!ModelState.IsValid || !items.Any())
        {
            if (!items.Any())
                ModelState.AddModelError("ItemsJson", "At least one observation or recommendation is required.");
            ViewBag.PlanId = id;
            return View("Create", model);
        }

        // Preserve the plan's original projectId — prevent cross-tenant re-assignment
        var projectId = existing.ProjectId;

        var dto = new
        {
            title = model.Title,
            description = model.Description,
            agentName = model.AgentName,
            agentUsername = model.AgentUsername,
            trainerName = model.TrainerName,
            trainerUsername = model.TrainerUsername,
            dueDate = model.DueDate,
            projectId
        };

        var ok = await _api.UpdateTrainingPlan(id, dto);
        if (!ok)
        {
            ModelState.AddModelError("", "Failed to update training plan.");
            ViewBag.PlanId = id;
            return View("Create", model);
        }

        TempData["Success"] = "Training plan updated.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    // ── Activate (Draft → Active) ─────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Activate(int id)
    {
        var plan = await _api.GetTrainingPlan(id);
        if (plan == null) return NotFound();
        if (!CanAccessPlan(plan)) return Forbid();

        var ok = await _api.UpdateTrainingPlanStatus(id,
            new { status = "Active", updatedBy = User.Identity?.Name ?? "admin" });
        TempData[ok ? "Success" : "Error"] = ok
            ? "Plan activated and now visible to agent and trainer."
            : "Failed to activate plan.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    // ── Start (Active → InProgress, by trainer) ───────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(int id)
    {
        var plan = await _api.GetTrainingPlan(id);
        if (plan == null) return NotFound();
        if (!CanAccessPlan(plan)) return Forbid();

        var ok = await _api.UpdateTrainingPlanStatus(id,
            new { status = "InProgress", updatedBy = User.Identity?.Name ?? "trainer" });
        TempData[ok ? "Success" : "Error"] = ok
            ? "Training marked as In Progress."
            : "Failed to update status.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    // ── CompleteItem (trainer marks an item done) ─────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteItem(int planId, int itemId, string? completionNotes)
    {
        var plan = await _api.GetTrainingPlan(planId);
        if (plan == null) return NotFound();
        if (!CanAccessPlan(plan)) return Forbid();

        var ok = await _api.CompleteTrainingPlanItem(planId, itemId,
            new { completedBy = User.Identity?.Name ?? "trainer", completionNotes });
        TempData[ok ? "Success" : "Error"] = ok
            ? "Item marked as done."
            : "Failed to update item.";
        return RedirectToAction(nameof(Detail), new { id = planId });
    }

    // ── Close (QM closes the loop) ────────────────────────────────────────────

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Close(int id)
    {
        var plan = await _api.GetTrainingPlan(id);
        if (plan == null) return NotFound();
        if (!CanAccessPlan(plan)) return Forbid();
        return View(new CloseTrainingPlanViewModel { PlanId = id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Close(CloseTrainingPlanViewModel model)
    {
        var plan = await _api.GetTrainingPlan(model.PlanId);
        if (plan == null) return NotFound();
        if (!CanAccessPlan(plan)) return Forbid();

        var result = await _api.CloseTrainingPlan(model.PlanId,
            new { closedBy = User.Identity?.Name ?? "admin", closingNotes = model.ClosingNotes });
        if (result == null)
        {
            TempData["Error"] = "Failed to close the training plan. Ensure the plan is at least InProgress.";
            return RedirectToAction(nameof(Detail), new { id = model.PlanId });
        }
        TempData["Success"] = "Training plan closed. The loop is complete.";
        return RedirectToAction(nameof(Detail), new { id = model.PlanId });
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var plan = await _api.GetTrainingPlan(id);
        if (plan == null) return NotFound();
        if (!CanAccessPlan(plan)) return Forbid();

        await _api.DeleteTrainingPlan(id);
        TempData["Success"] = "Training plan deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when the current user's project can access the plan.
    /// Access is always granted when no project is selected (CurrentProjectId == 0),
    /// or when the plan has no project association, or when the IDs match.
    /// </summary>
    private bool CanAccessPlan(TrainingPlanViewModel plan) =>
        CurrentProjectId == 0 || plan.ProjectId == null || plan.ProjectId == CurrentProjectId;

    private static string? BuildDefaultDescription(string? formName, string? aiReasoning)
    {
        if (string.IsNullOrWhiteSpace(formName) && string.IsNullOrWhiteSpace(aiReasoning))
            return null;
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(formName))
            parts.Add($"Form: {formName}");
        if (!string.IsNullOrWhiteSpace(aiReasoning))
            parts.Add($"AI Notes: {aiReasoning}");
        return string.Join("\n\n", parts);
    }

    private sealed class PlanIdComparer : IEqualityComparer<TrainingPlanViewModel>
    {
        public bool Equals(TrainingPlanViewModel? x, TrainingPlanViewModel? y) => x?.Id == y?.Id;
        public int GetHashCode(TrainingPlanViewModel obj) => obj.Id.GetHashCode();
    }
}
