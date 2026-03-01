using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAAutomation.Web.Models;
using QAAutomation.Web.Services;
using System.Text.Json;

namespace QAAutomation.Web.Controllers;

[Authorize]
public class RatingCriteriaController : Controller
{
    private readonly ApiClient _api;

    public RatingCriteriaController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index()
    {
        var items = await _api.GetRatingCriteria();
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Designer(int? id)
    {
        RatingCriteriaViewModel vm;
        if (id.HasValue)
            vm = await _api.GetRatingCriteriaById(id.Value) ?? new RatingCriteriaViewModel();
        else
            vm = new RatingCriteriaViewModel { MinScore = 1, MaxScore = 5 };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Designer(int? id, string criteriaName, string? criteriaDescription,
        int minScore, int maxScore, string levelsJson)
    {
        var levels = new List<object>();
        if (!string.IsNullOrEmpty(levelsJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<JsonElement>>(levelsJson);
                if (parsed != null)
                {
                    levels = parsed.Select(e => (object)new
                    {
                        score = e.GetProperty("score").GetInt32(),
                        label = e.GetProperty("label").GetString() ?? "",
                        description = e.TryGetProperty("description", out var d) && d.ValueKind != JsonValueKind.Null ? d.GetString() : null,
                        color = e.TryGetProperty("color", out var c) ? c.GetString() ?? "#6c757d" : "#6c757d"
                    }).ToList();
                }
            }
            catch { }
        }

        var dto = new
        {
            name = criteriaName,
            description = criteriaDescription,
            minScore,
            maxScore,
            isActive = true,
            levels
        };

        if (id.HasValue)
            await _api.UpdateRatingCriteria(id.Value, dto);
        else
            await _api.CreateRatingCriteria(dto);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _api.DeleteRatingCriteria(id);
        return RedirectToAction(nameof(Index));
    }
}
