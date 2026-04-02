using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using QAAutomation.Web.Services;

namespace QAAutomation.Web.Filters;

/// <summary>
/// Global action filter that injects the current LLM provider name and display metadata
/// into ViewData for every authenticated page request, so _Layout.cshtml can render the
/// appropriate provider badge without each individual controller needing to supply it.
/// The provider value is cached for 5 minutes to avoid an API round-trip on every load.
/// </summary>
public class LlmProviderFilter : IAsyncActionFilter
{
    public const string CacheKey = "LlmProvider_Current";
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);

    private readonly ApiClient _api;
    private readonly IMemoryCache _cache;

    public LlmProviderFilter(ApiClient api, IMemoryCache cache)
    {
        _api   = api;
        _cache = cache;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.Controller is Controller controller
            && context.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            if (!_cache.TryGetValue(CacheKey, out string? provider))
            {
                try
                {
                    var settings = await _api.GetAiSettings();
                    provider = settings?.LlmProvider ?? "";
                }
                catch
                {
                    provider = "";
                }
                _cache.Set(CacheKey, provider ?? "", CacheExpiry);
            }

            var (displayName, iconClass, badgeCss) = ResolveProvider(provider ?? "");
            controller.ViewData["LlmProvider"]    = provider ?? "";
            controller.ViewData["LlmDisplayName"] = displayName;
            controller.ViewData["LlmIconClass"]   = iconClass;
            controller.ViewData["LlmBadgeCss"]    = badgeCss;
        }

        await next();
    }

    /// <summary>Maps the raw provider key to human-readable metadata used in the UI.</summary>
    public static (string DisplayName, string IconClass, string BadgeCss) ResolveProvider(string provider) =>
        provider switch
        {
            "Google"      => ("Gemini AI",    "bi-stars",      "background:linear-gradient(135deg,#4285F4,#9B72CB);color:#fff"),
            "AzureOpenAI" => ("Azure OpenAI", "bi-cloud-fill", "background:#0078d4;color:#fff"),
            "OpenAI"      => ("OpenAI",       "bi-robot",      "background:#10a37f;color:#fff"),
            _             => ("Simulated",    "bi-gear",       "background:#6c757d;color:#fff"),
        };
}
