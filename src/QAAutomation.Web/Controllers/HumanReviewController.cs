using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAAutomation.Web.Models;
using QAAutomation.Web.Services;

namespace QAAutomation.Web.Controllers;

/// <summary>
/// Web UI for the human review queue.
/// QA analysts see sampled calls, view the AI audit result, and submit their verdict.
/// Admins can also access this and manually enqueue items.
/// </summary>
[Authorize(Roles = "Admin,QA")]
public class HumanReviewController : ProjectAwareController
{
    private readonly ApiClient _api;

    public HumanReviewController(ApiClient api) => _api = api;

    // ── Index — review queue ──────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index(string? status = null)
    {
        // QA users see only items assigned to them or unassigned
        // Admin sees everything
        string? assignedFilter = null;
        if (!User.IsInRole("Admin"))
            assignedFilter = User.Identity?.Name;

        var pid = CurrentProjectId > 0 ? (int?)CurrentProjectId : null;
        var items = await _api.GetReviewQueue(status: status, assignedTo: assignedFilter, projectId: pid);
        ViewBag.StatusFilter = status;
        return View(items);
    }

    // ── Review — view AI audit and submit verdict ─────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Review(int id)
    {
        var item = await _api.GetReviewItem(id);
        if (item == null) return NotFound();

        // Mark as InReview when opened
        if (item.Status == "Pending")
            await _api.StartReview(id, User.Identity?.Name ?? "qa");

        // Load the full audit record so the view can render per-field scores,
        // sentiment analysis, and coaching recommendations (same as Audit/Detail).
        var audit = await _api.GetAudit(item.EvaluationResultId);

        var vm = new SubmitReviewViewModel
        {
            ReviewItemId = id,
            ReviewVerdict = "Agree"
        };

        ViewBag.Item = item;
        ViewBag.Audit = audit;
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(SubmitReviewViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var item = await _api.GetReviewItem(model.ReviewItemId);
            var audit = item != null ? await _api.GetAudit(item.EvaluationResultId) : null;
            ViewBag.Item = item;
            ViewBag.Audit = audit;
            return View(model);
        }

        var dto = new
        {
            reviewerComment = model.ReviewerComment,
            reviewVerdict = model.ReviewVerdict,
            reviewedBy = User.Identity?.Name ?? "qa"
        };

        var ok = await _api.SubmitReview(model.ReviewItemId, dto);
        if (!ok)
        {
            ModelState.AddModelError("", "Failed to submit review. Please try again.");
            var item = await _api.GetReviewItem(model.ReviewItemId);
            var audit = item != null ? await _api.GetAudit(item.EvaluationResultId) : null;
            ViewBag.Item = item;
            ViewBag.Audit = audit;
            return View(model);
        }

        TempData["Success"] = "Review submitted successfully.";
        return RedirectToAction(nameof(Index));
    }
}
