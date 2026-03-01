using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAAutomation.Web.Models;
using QAAutomation.Web.Services;

namespace QAAutomation.Web.Controllers;

[Authorize]
public class ParametersController : Controller
{
    private readonly ApiClient _api;

    public ParametersController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index()
    {
        var items = await _api.GetParameters();
        return View(items);
    }

    [HttpGet]
    public IActionResult Create() => View(new CreateParameterViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateParameterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var success = await _api.CreateParameter(new
        {
            model.Name, model.Description, model.Category, model.DefaultWeight, model.EvaluationType
        });
        if (!success) { ModelState.AddModelError("", "Failed to create parameter."); return View(model); }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var item = await _api.GetParameter(id);
        if (item is null) return NotFound();
        var vm = new CreateParameterViewModel
        {
            Name = item.Name, Description = item.Description,
            Category = item.Category, DefaultWeight = item.DefaultWeight,
            IsActive = item.IsActive, EvaluationType = item.EvaluationType
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CreateParameterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var success = await _api.UpdateParameter(id, new
        {
            model.Name, model.Description, model.Category,
            model.DefaultWeight, model.IsActive, model.EvaluationType
        });
        if (!success) { ModelState.AddModelError("", "Failed to update parameter."); return View(model); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _api.DeleteParameter(id);
        return RedirectToAction(nameof(Index));
    }
}
