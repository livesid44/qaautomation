using QAAutomation.API.Services;
using Xunit;

namespace QAAutomation.Tests;

/// <summary>
/// Unit tests for the URL-ingestion helpers added to <see cref="KnowledgeBaseService"/>.
/// These cover HTML extraction and URL validation — the HTTP fetch itself is tested via
/// the integration layer.
/// </summary>
public class KnowledgeUrlFetchTests
{
    // ── ExtractTextFromHtml ───────────────────────────────────────────────────

    [Fact]
    public void ExtractTextFromHtml_PlainParagraph_ReturnsText()
    {
        const string html = "<html><body><p>Hello World</p></body></html>";
        var result = KnowledgeBaseService.ExtractTextFromHtml(html);
        Assert.Contains("Hello World", result);
    }

    [Fact]
    public void ExtractTextFromHtml_ScriptBlockRemoved()
    {
        const string html = "<html><body><script>alert('xss');</script><p>Policy text here.</p></body></html>";
        var result = KnowledgeBaseService.ExtractTextFromHtml(html);
        Assert.DoesNotContain("alert", result);
        Assert.Contains("Policy text here", result);
    }

    [Fact]
    public void ExtractTextFromHtml_StyleBlockRemoved()
    {
        const string html = "<html><head><style>body { color: red; }</style></head><body><p>Visible text.</p></body></html>";
        var result = KnowledgeBaseService.ExtractTextFromHtml(html);
        Assert.DoesNotContain("color:", result);
        Assert.Contains("Visible text", result);
    }

    [Fact]
    public void ExtractTextFromHtml_HtmlEntitiesDecoded()
    {
        const string html = "<p>Creators &amp; agents must comply with the YouTube&nbsp;policy.</p>";
        var result = KnowledgeBaseService.ExtractTextFromHtml(html);
        Assert.Contains("Creators & agents", result);
        Assert.Contains("YouTube", result);
    }

    [Fact]
    public void ExtractTextFromHtml_EmptyOrWhitespace_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, KnowledgeBaseService.ExtractTextFromHtml(""));
        Assert.Equal(string.Empty, KnowledgeBaseService.ExtractTextFromHtml("   "));
    }

    [Fact]
    public void ExtractTextFromHtml_RealWorldLikeHtml_ExtractsBody()
    {
        const string html = """
            <!DOCTYPE html>
            <html>
              <head>
                <title>YouTube Partner Programme Policy</title>
                <style>.nav { display:none }</style>
                <script src="app.js"></script>
              </head>
              <body>
                <nav><a href="/">Home</a></nav>
                <main>
                  <h1>Eligibility</h1>
                  <p>Channels must have at least 1,000 subscribers and 4,000 watch hours in the past 12 months.</p>
                </main>
              </body>
            </html>
            """;

        var result = KnowledgeBaseService.ExtractTextFromHtml(html);

        Assert.Contains("Eligibility", result);
        Assert.Contains("1,000 subscribers", result);
        Assert.DoesNotContain("display:none", result);
        Assert.DoesNotContain("src=\"app.js\"", result);
    }

    [Fact]
    public void ExtractTextFromHtml_MultilineScript_FullyRemoved()
    {
        const string html = """
            <body>
            <script>
              var secret = "do not capture this";
              function init() { return 42; }
            </script>
            <p>Captured content.</p>
            </body>
            """;

        var result = KnowledgeBaseService.ExtractTextFromHtml(html);
        Assert.DoesNotContain("secret", result);
        Assert.DoesNotContain("do not capture", result);
        Assert.Contains("Captured content", result);
    }

    // ── URL validation ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com/file.txt")]
    [InlineData("")]
    public void UrlValidation_InvalidOrUnsupportedScheme_FailsValidation(string url)
    {
        bool isValid = Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                       (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        Assert.False(isValid, $"Expected '{url}' to be invalid or unsupported but it was accepted.");
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://support.google.com/youtube/answer/72851")]
    public void UrlValidation_ValidHttpUrl_PassesValidation(string url)
    {
        bool isValid = Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                       (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        Assert.True(isValid, $"Expected '{url}' to be valid but it was rejected.");
    }

    // ── SSRF guard ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("localhost")]
    [InlineData("ip6-localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("10.255.255.255")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.100")]
    [InlineData("169.254.169.254")]   // AWS metadata endpoint
    public void IsPrivateOrLocalHost_PrivateAddresses_ReturnsTrue(string host)
    {
        Assert.True(KnowledgeBaseService.IsPrivateOrLocalHost(host),
            $"Expected '{host}' to be blocked as private/loopback but it was permitted.");
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("support.google.com")]
    [InlineData("8.8.8.8")]          // Google DNS — public IP
    [InlineData("93.184.216.34")]    // example.com IP — public
    public void IsPrivateOrLocalHost_PublicAddresses_ReturnsFalse(string host)
    {
        Assert.False(KnowledgeBaseService.IsPrivateOrLocalHost(host),
            $"Expected '{host}' to be permitted as a public address but it was blocked.");
    }
}
