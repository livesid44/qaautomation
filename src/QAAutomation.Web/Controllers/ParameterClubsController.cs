using Microsoft.AspNetCore.Mvc;
using QAAutomation.Web.Models;
using QAAutomation.Web.Services;
using System.Text.Json;

namespace QAAutomation.Web.Controllers;

public class ParameterClubsController : ProjectAwareController
{
    private readonly ApiClient _api;

    public ParameterClubsController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index()
    {
        var clubs = await _api.GetParameterClubs();
        return View(clubs);
    }

    [HttpGet]
    public async Task<IActionResult> Designer(int? id)
    {
        var allParameters = await _api.GetParameters();
        var allCriteria = await _api.GetRatingCriteria();
        ParameterClubViewModel club;
        if (id.HasValue)
        {
            club = await _api.GetParameterClub(id.Value) ?? new ParameterClubViewModel();
        }
        else
        {
            club = new ParameterClubViewModel();
        }
        ViewBag.AllParameters = allParameters;
        ViewBag.AllCriteria = allCriteria;
        return View(club);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Designer(int? id, string clubName, string? clubDescription, string itemsJson)
    {
        List<object> items = new();
        if (!string.IsNullOrEmpty(itemsJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<JsonElement>>(itemsJson);
                if (parsed != null)
                {
                    items = parsed.Select(e => (object)new
                    {
                        parameterId = e.GetProperty("parameterId").GetInt32(),
                        order = e.TryGetProperty("order", out var o) ? o.GetInt32() : 0,
                        weightOverride = e.TryGetProperty("weightOverride", out var w) && w.ValueKind != JsonValueKind.Null ? (double?)w.GetDouble() : null,
                        ratingCriteriaId = e.TryGetProperty("ratingCriteriaId", out var r) && r.ValueKind != JsonValueKind.Null ? (int?)r.GetInt32() : null
                    }).ToList();
                }
            }
            catch { }
        }

        if (id.HasValue)
        {
            await _api.UpdateParameterClub(id.Value, new { name = clubName, description = clubDescription, isActive = true });
            await _api.UpdateClubItems(id.Value, items);
        }
        else
        {
            var created = await _api.CreateParameterClub(new
            {
                name = clubName,
                description = clubDescription,
                projectId = CurrentProjectId > 0 ? CurrentProjectId : (int?)null
            });
            if (created != null)
                await _api.UpdateClubItems(created.Id, items);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _api.DeleteParameterClub(id);
        return RedirectToAction(nameof(Index));
    }
}
