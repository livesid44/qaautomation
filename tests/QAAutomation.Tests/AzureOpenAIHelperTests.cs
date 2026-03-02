using QAAutomation.API.Services;
using Xunit;

namespace QAAutomation.Tests;

public class AzureOpenAIHelperTests
{
    // ── NormalizeEndpoint ──────────────────────────────────────────────────────

    [Fact]
    public void NormalizeEndpoint_FullDeploymentUrl_ExtractsDeploymentAndBuildsFoundryEndpoint()
    {
        // Full URL copy-pasted from Azure Portal (traditional format)
        var (ep, dep) = AzureOpenAIHelper.NormalizeEndpoint(
            "https://sp00534844-8489-resource.cognitiveservices.azure.com/openai/deployments/gpt-4o-2/chat/completions?api-version=2025-01-01-preview",
            "");

        Assert.Equal("https://sp00534844-8489-resource.cognitiveservices.azure.com/openai/v1/", ep);
        Assert.Equal("gpt-4o-2", dep); // deployment extracted from URL
    }

    [Fact]
    public void NormalizeEndpoint_FullDeploymentUrl_ExplicitDeploymentWinsOverUrl()
    {
        // Separately-configured LlmDeployment takes priority over what's in the URL
        var (ep, dep) = AzureOpenAIHelper.NormalizeEndpoint(
            "https://sp00534844-8489-resource.cognitiveservices.azure.com/openai/deployments/gpt-4o-2/chat/completions?api-version=2025-01-01-preview",
            "gpt-4o");

        Assert.Equal("https://sp00534844-8489-resource.cognitiveservices.azure.com/openai/v1/", ep);
        Assert.Equal("gpt-4o", dep); // explicit deployment wins
    }

    [Fact]
    public void NormalizeEndpoint_FoundryV1Endpoint_IsUsedAsIs()
    {
        // Already in the correct Foundry format — no mutation needed
        var (ep, dep) = AzureOpenAIHelper.NormalizeEndpoint(
            "https://sp00534844-8489-resource.cognitiveservices.azure.com/openai/v1/",
            "gpt-4o");

        Assert.Equal("https://sp00534844-8489-resource.cognitiveservices.azure.com/openai/v1/", ep);
        Assert.Equal("gpt-4o", dep);
    }

    [Fact]
    public void NormalizeEndpoint_PlainBaseUrl_AppendsFoundryPath()
    {
        // User enters only the resource root; the helper appends /openai/v1/
        var (ep, dep) = AzureOpenAIHelper.NormalizeEndpoint(
            "https://resource.cognitiveservices.azure.com/",
            "gpt-4o");

        Assert.Equal("https://resource.cognitiveservices.azure.com/openai/v1/", ep);
        Assert.Equal("gpt-4o", dep);
    }

    [Fact]
    public void NormalizeEndpoint_PlainBaseUrlNoTrailingSlash_AppendsFoundryPath()
    {
        var (ep, _) = AzureOpenAIHelper.NormalizeEndpoint(
            "https://resource.cognitiveservices.azure.com",
            "gpt-4o");

        Assert.Equal("https://resource.cognitiveservices.azure.com/openai/v1/", ep);
    }

    [Fact]
    public void NormalizeEndpoint_AlwaysEndsWithTrailingSlash()
    {
        var (ep, _) = AzureOpenAIHelper.NormalizeEndpoint(
            "https://resource.cognitiveservices.azure.com/openai/deployments/gpt-4o/chat/completions",
            "gpt-4o");

        Assert.EndsWith("/", ep);
    }

    [Fact]
    public void NormalizeEndpoint_NullEndpoint_ReturnsUnchanged()
    {
        var (ep, dep) = AzureOpenAIHelper.NormalizeEndpoint(null!, "gpt-4o");

        Assert.Null(ep);
        Assert.Equal("gpt-4o", dep);
    }

    [Fact]
    public void NormalizeEndpoint_EmptyEndpoint_ReturnsUnchanged()
    {
        var (ep, dep) = AzureOpenAIHelper.NormalizeEndpoint("", "gpt-4o");

        Assert.Equal("", ep);
        Assert.Equal("gpt-4o", dep);
    }
}
