using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAAutomation.Web.Models;
using QAAutomation.Web.Services;

namespace QAAutomation.Web.Controllers;

/// <summary>
/// Admin UI for managing <see cref="SamplingPolicyViewModel"/> records.
/// Allows tenant-level admins to define how many / what percentage of completed
/// AI-audited calls should be sent to the human review queue.
/// </summary>
[Authorize(Roles = "Admin")]
public class SamplingController : Controller
{
    private readonly ApiClient _api;

    public SamplingController(ApiClient api) => _api = api;

    // ── Index ─────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var policies = await _api.GetSamplingPolicies();
        return View(policies);
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateSamplingPolicyViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateSamplingPolicyViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var dto = new
        {
            name = model.Name,
            description = model.Description,
            callTypeFilter = model.CallTypeFilter,
            minDurationSeconds = model.MinDurationSeconds,
            maxDurationSeconds = model.MaxDurationSeconds,
            samplingMethod = model.SamplingMethod,
            sampleValue = model.SampleValue,
            isActive = model.IsActive,
            createdBy = User.Identity?.Name ?? "admin"
        };

        var created = await _api.CreateSamplingPolicy(dto);
        if (created == null)
        {
            ModelState.AddModelError("", "Failed to create sampling policy — please try again.");
            return View(model);
        }

        TempData["Success"] = $"Sampling policy '{model.Name}' created.";
        return RedirectToAction(nameof(Index));
    }

    // ── Edit ──────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var policy = await _api.GetSamplingPolicy(id);
        if (policy == null) return NotFound();

        var vm = new CreateSamplingPolicyViewModel
        {
            Name = policy.Name,
            Description = policy.Description,
            CallTypeFilter = policy.CallTypeFilter,
            MinDurationSeconds = policy.MinDurationSeconds,
            MaxDurationSeconds = policy.MaxDurationSeconds,
            SamplingMethod = policy.SamplingMethod,
            SampleValue = policy.SampleValue,
            IsActive = policy.IsActive
        };
        ViewBag.PolicyId = id;
        return View("Create", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CreateSamplingPolicyViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.PolicyId = id;
            return View("Create", model);
        }

        var dto = new
        {
            name = model.Name,
            description = model.Description,
            callTypeFilter = model.CallTypeFilter,
            minDurationSeconds = model.MinDurationSeconds,
            maxDurationSeconds = model.MaxDurationSeconds,
            samplingMethod = model.SamplingMethod,
            sampleValue = model.SampleValue,
            isActive = model.IsActive
        };

        var ok = await _api.UpdateSamplingPolicy(id, dto);
        if (!ok)
        {
            ModelState.AddModelError("", "Failed to update policy — please try again.");
            ViewBag.PolicyId = id;
            return View("Create", model);
        }

        TempData["Success"] = $"Sampling policy '{model.Name}' updated.";
        return RedirectToAction(nameof(Index));
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _api.DeleteSamplingPolicy(id);
        TempData["Success"] = "Sampling policy deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ── Apply ─────────────────────────────────────────────────────────────────

    /// <summary>Applies the policy — samples eligible evaluations into the review queue.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(int id)
    {
        var result = await _api.ApplySamplingPolicy(id, User.Identity?.Name ?? "admin");
        if (result == null)
        {
            TempData["Error"] = "Failed to apply sampling policy. Please try again.";
        }
        else
        {
            TempData["Success"] = "Sampling policy applied — new items added to the review queue.";
        }
        return RedirectToAction(nameof(Index));
    }
}
