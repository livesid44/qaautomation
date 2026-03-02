using Microsoft.AspNetCore.Authentication.Cookies;
using QAAutomation.Web.Services;

var builder = WebApplication.CreateBuilder(args);

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5018";

// Port 7083 is the API's HTTPS-only Kestrel endpoint (see API launchSettings "https" profile).
// If ApiBaseUrl was configured with a plain-HTTP scheme pointing at that port – e.g. via a
// machine-level environment variable or a VS launch configuration that strips the scheme –
// the TLS handshake never starts and Kestrel drops the connection with
// "The response ended prematurely".  Upgrade the scheme here so the fix is code-level
// and works regardless of how ApiBaseUrl was set.
if (Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var parsedApiUrl)
    && parsedApiUrl.Scheme == Uri.UriSchemeHttp
    && parsedApiUrl.Port == 7083)
{
    apiBaseUrl = new UriBuilder(parsedApiUrl) { Scheme = Uri.UriSchemeHttps, Port = parsedApiUrl.Port }.Uri.ToString();
}

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpClient<ApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    // In Development the API uses a self-signed localhost dev certificate.
    // Accept it without requiring OS-level trust so developers don't need
    // to run 'dotnet dev-certs https --trust' before the Web app can talk to the API.
    if (builder.Environment.IsDevelopment())
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    return handler;
});

builder.Services.AddControllersWithViews(options =>
{
    // The Web app is a thin UI layer over the API; the API does all real validation.
    // Non-nullable string properties should accept empty strings from forms (e.g. LlmApiKey
    // submitted empty means "keep the existing key"). Suppress the ASP.NET Core 6+ implicit
    // [Required] behavior that would otherwise reject empty-string form fields.
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
})
// Store TempData in the server-side session instead of a cookie.
// The AI analysis result (AutoAuditReview) can be several kilobytes; keeping it
// in a cookie causes HTTP 400 "Request Too Long" when the browser sends it back
// in request headers.  Session storage replaces the data cookie with a tiny
// session-ID cookie, eliminating the header-size overflow.
.AddSessionStateTempDataProvider();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

var pathBase = app.Configuration["PathBase"];
if (!string.IsNullOrEmpty(pathBase))
    app.UsePathBase(pathBase);

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
