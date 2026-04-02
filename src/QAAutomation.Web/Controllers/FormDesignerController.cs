using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAAutomation.Web.Models;
using QAAutomation.Web.Services;
using System.Text.Json;

namespace QAAutomation.Web.Controllers;

[Authorize]
public class FormDesignerController : ProjectAwareController
{
    private readonly ApiClient _api;

    public FormDesignerController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index()
    {
        var pid = CurrentProjectId > 0 ? (int?)CurrentProjectId : null;
        var forms = await _api.GetEvaluationForms(pid);
        return View(forms);
    }

    [HttpGet]
    public async Task<IActionResult> Designer(int? id)
    {
        var pid = CurrentProjectId > 0 ? (int?)CurrentProjectId : null;
        var clubs = await _api.GetParameterClubs(pid);
        var parameters = await _api.GetParameters(pid);
        var criteria = await _api.GetRatingCriteria(pid);

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
            var formId = formData.TryGetProperty("id", out var fi) ? fi.GetInt32() : 0;
            var name = formData.GetProperty("name").GetString() ?? "Unnamed Form";
            var description = formData.TryGetProperty("description", out var d) ? d.GetString() : null;
            var scoringMethod = formData.TryGetProperty("scoringMethod", out var sm) ? sm.GetInt32() : 0;
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

            if (formId > 0)
            {
                // Update existing form (name, description, scoringMethod only — sections managed separately)
                var updateDto = new { name, description, isActive = true, lobId = (int?)null, scoringMethod };
                await _api.UpdateEvaluationForm(formId, updateDto);
            }
            else
            {
                var dto = new { name, description, scoringMethod, sections = sectionList };
                await _api.SaveEvaluationForm(dto);
            }
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
