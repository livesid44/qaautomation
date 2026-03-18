using Microsoft.AspNetCore.Mvc;
using QAAutomation.Web.Models;
using QAAutomation.Web.Services;
using System.Text.Json;

namespace QAAutomation.Web.Controllers;

public class AuditController : ProjectAwareController
{
    private readonly ApiClient _api;

    public AuditController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index(int? formId)
    {
        var pid = CurrentProjectId > 0 ? (int?)CurrentProjectId : null;
        var audits = formId.HasValue
            ? await _api.GetAuditsByForm(formId.Value)
            : await _api.GetAudits(pid);
        var forms = await _api.GetLegacyForms(pid);
        ViewBag.Forms = forms;
        ViewBag.SelectedFormId = formId;
        return View(audits);
    }

    [HttpGet]
    public async Task<IActionResult> New(int? formId)
    {
        var pid = CurrentProjectId > 0 ? (int?)CurrentProjectId : null;
        var forms = await _api.GetLegacyForms(pid);
        ViewBag.Forms = forms;

        LegacyFormViewModel? selectedForm = null;
        if (formId.HasValue)
            selectedForm = await _api.GetLegacyForm(formId.Value);
        else if (forms.Any())
            selectedForm = await _api.GetLegacyForm(forms.First().Id);

        ViewBag.SelectedForm = selectedForm;
        ViewBag.SelectedFormId = selectedForm?.Id;
        return View(new NewAuditViewModel { FormId = selectedForm?.Id ?? 0, EvaluatedBy = User.Identity?.Name ?? "" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> New(NewAuditViewModel model, string scoresJson)
    {
        var scores = new List<object>();
        if (!string.IsNullOrEmpty(scoresJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<JsonElement>>(scoresJson);
                if (parsed != null)
                {
                scores = parsed
                    .Where(e => e.TryGetProperty("fieldId", out _))
                    .Select(e =>
                    {
                        var fieldId = e.GetProperty("fieldId").GetInt32();
                        var val = e.TryGetProperty("value", out var v) ? v.GetString() ?? "0" : "0";
                        double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var numVal);
                        return (object)new { fieldId, value = val, numericValue = numVal };
                    }).ToList();
                }
            }
            catch { }
        }

        var dto = new
        {
            formId = model.FormId,
            evaluatedBy = model.EvaluatedBy,
            agentName = model.AgentName,
            callReference = model.CallReference,
            callDate = model.CallDate,
            notes = model.Notes,
            scores
        };

        await _api.CreateAudit(dto);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var audit = await _api.GetAudit(id);
        if (audit == null) return NotFound();
        var form = await _api.GetLegacyForm(audit.FormId);
        ViewBag.Form = form;
        return View(audit);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _api.DeleteAudit(id);
        return RedirectToAction(nameof(Index));
    }
}
