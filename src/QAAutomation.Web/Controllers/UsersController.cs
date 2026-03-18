using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAAutomation.Web.Models;
using QAAutomation.Web.Services;

namespace QAAutomation.Web.Controllers;

[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private readonly ApiClient _api;

    public UsersController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index()
    {
        var users = await _api.GetUsers();
        return View(users);
    }

    [HttpGet]
    public IActionResult Create() => View(new CreateUserViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var success = await _api.CreateUser(new
        {
            model.Username, model.Password, model.Email, model.Role
        });
        if (!success) { ModelState.AddModelError("", "Failed to create user."); return View(model); }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _api.GetUser(id);
        if (user is null) return NotFound();
        var vm = new EditUserViewModel
        {
            Id = user.Id, Username = user.Username, Email = user.Email,
            Role = user.Role, IsActive = user.IsActive
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditUserViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var success = await _api.UpdateUser(id, new
        {
            model.Email, model.Role, model.IsActive, model.Password
        });
        if (!success) { ModelState.AddModelError("", "Failed to update user."); return View(model); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _api.DeleteUser(id);
        return RedirectToAction(nameof(Index));
    }
}
