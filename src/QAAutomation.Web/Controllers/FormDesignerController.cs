using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAAutomation.Web.Models;
using QAAutomation.Web.Services;
using System.Text.Json;

namespace QAAutomation.Web.Controllers;

[Authorize]
public class FormDesignerController : Controller
{
    private readonly ApiClient _api;

    public FormDesignerController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index()
    {
        var forms = await _api.GetEvaluationForms();
        return View(forms);
    }

    [HttpGet]
    public async Task<IActionResult> Designer(int? id)
    {
        var clubs = await _api.GetParameterClubs();
        var parameters = await _api.GetParameters();
        var criteria = await _api.GetRatingCriteria();

        EvaluationFormViewModel form;
        if (id.HasValue)
            form = await _api.GetEvaluationForm(id.Value) ?? new EvaluationFormViewModel();
        else
            form = new EvaluationFormViewModel();

        var vm = new FormDesignerViewModel
        {
            Form = form,
            AvailableClubs = clubs,
            AllParameters = parameters,
            AllCriteria = criteria
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Designer(string formJson)
    {
        if (string.IsNullOrEmpty(formJson))
            return RedirectToAction(nameof(Index));

        try
        {
            var formData = JsonSerializer.Deserialize<JsonElement>(formJson);
            var name = formData.GetProperty("name").GetString() ?? "Unnamed Form";
            var description = formData.TryGetProperty("description", out var d) ? d.GetString() : null;
            var sections = formData.TryGetProperty("sections", out var s) ? s : default;

            var sectionList = new List<object>();
            if (sections.ValueKind == JsonValueKind.Array)
            {
                int sectionOrder = 0;
                foreach (var section in sections.EnumerateArray())
                {
                    var title = section.TryGetProperty("title", out var t) ? t.GetString() ?? "Section" : "Section";
                    var fields = new List<object>();
                    if (section.TryGetProperty("fields", out var fArr) && fArr.ValueKind == JsonValueKind.Array)
                    {
                        int fieldOrder = 0;
                        foreach (var field in fArr.EnumerateArray())
                        {
                            var label = field.TryGetProperty("parameterName", out var pn) ? pn.GetString() ?? "" : "";
                            var fieldType = "Rating";
                            fields.Add(new { label, fieldType, isRequired = true, order = fieldOrder++, options = (string?)null, maxRating = 5 });
                        }
                    }
                    sectionList.Add(new { title, description = (string?)null, order = sectionOrder++, fields });
                }
            }

            var dto = new { name, description, sections = sectionList };
            await _api.SaveEvaluationForm(dto);
        }
        catch { }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _api.DeleteEvaluationForm(id);
        return RedirectToAction(nameof(Index));
    }
}
