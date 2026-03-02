using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAAutomation.Web.Models;
using QAAutomation.Web.Services;

namespace QAAutomation.Web.Controllers;

/// <summary>Admin UI for configuring LLM, sentiment provider, and RAG settings stored in the database.</summary>
[Authorize(Roles = "Admin")]
public class AiSettingsController : Controller
{
    private readonly ApiClient _api;
    private readonly ILogger<AiSettingsController> _logger;

    public AiSettingsController(ApiClient api, ILogger<AiSettingsController> logger)
    {
        _api = api;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var settings = await _api.GetAiSettings();
        if (settings == null)
        {
            TempData["Error"] = "Could not load AI settings from the API — please check the API connection.";
            settings = new AiSettingsViewModel();
        }
        // Track whether keys are currently configured (before blanking them for display)
        ViewData["LlmKeyIsSet"] = settings.LlmApiKey == "***";
        ViewData["LangKeyIsSet"] = settings.LanguageApiKey == "***";
        ViewData["SpeechKeyIsSet"] = settings.SpeechApiKey == "***";
        // Never display masked keys — show blank so admin knows they can enter a new value
        if (settings.LlmApiKey == "***") settings.LlmApiKey = "";
        if (settings.LanguageApiKey == "***") settings.LanguageApiKey = "";
        if (settings.SpeechApiKey == "***") settings.SpeechApiKey = "";
        ViewData["ApiBaseUrl"] = _api.BaseUrl;
        return View(settings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(AiSettingsViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Where(e => e.Value?.Errors.Count > 0)
                .Select(e => $"{e.Key}: {string.Join(", ", e.Value!.Errors.Select(x => x.ErrorMessage))}");
            _logger.LogWarning("AiSettings POST: ModelState invalid — {Errors}", string.Join("; ", errors));
            return View(model);
        }

        var ok = await _api.SaveAiSettings(model);
        if (ok)
        {
            TempData["Success"] = "AI settings saved successfully.";
        }
        else
        {
            TempData["Error"] = "Failed to save settings — please check the API connection.";
        }
        return RedirectToAction(nameof(Index));
    }
}
