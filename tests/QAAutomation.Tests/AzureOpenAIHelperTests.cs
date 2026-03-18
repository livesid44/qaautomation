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

public class AutoAuditSystemPromptTests
{
    private static AutoAuditFieldDefinition Field(int id, string label, string section,
        int max = 5, string type = "LLM", string? desc = null) =>
        new(id, label, desc, max, false, section, type);

    // ── [KB] tag only appears when context is available ────────────────────────

    [Fact]
    public void BuildSystemPrompt_KbFieldWithContext_TaggedKB()
    {
        var fields = new List<AutoAuditFieldDefinition>
        {
            Field(1, "CFPB Compliance", "Compliance", max: 1, type: "KnowledgeBased")
        };
        var kbMap = new Dictionary<int, string> { [1] = "Regulation text here" };

        var prompt = AzureOpenAIAutoAuditService.BuildSystemPrompt("Test Form", fields, kbMap);

        Assert.Contains("[KB]", prompt);
        Assert.Contains("KNOWLEDGE BASE CONTEXT", prompt);
        Assert.Contains("Regulation text here", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_KbFieldWithoutContext_NotTaggedKB()
    {
        // KnowledgeBased field but NO documents uploaded — should score via LLM general knowledge
        var fields = new List<AutoAuditFieldDefinition>
        {
            Field(1, "CFPB Compliance", "Compliance", max: 1, type: "KnowledgeBased")
        };
        var emptyKbMap = new Dictionary<int, string>(); // no chunks retrieved

        var prompt = AzureOpenAIAutoAuditService.BuildSystemPrompt("Test Form", fields, emptyKbMap);

        Assert.DoesNotContain("[KB]", prompt);
        Assert.DoesNotContain("KNOWLEDGE BASE CONTEXT", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_MixedFields_OnlyContextualFieldTaggedKB()
    {
        // Field 1: KnowledgeBased WITH context → [KB]
        // Field 2: KnowledgeBased WITHOUT context → no tag, scored by LLM
        // Field 3: LLM type → no tag
        var fields = new List<AutoAuditFieldDefinition>
        {
            Field(1, "PCI DSS", "Compliance", max: 1, type: "KnowledgeBased"),
            Field(2, "CFPB Rules", "Compliance", max: 1, type: "KnowledgeBased"),
            Field(3, "Empathy", "Communication", max: 5, type: "LLM"),
        };
        var kbMap = new Dictionary<int, string> { [1] = "PCI policy document" }; // only field 1 has context

        var prompt = AzureOpenAIAutoAuditService.BuildSystemPrompt("Test Form", fields, kbMap);

        // Field 1 has KB context → gets [KB] tag in the FORM FIELDS section
        Assert.Contains("\"PCI DSS\" [KB]", prompt);
        // Field 2 has no KB context → no [KB] tag
        Assert.DoesNotContain("\"CFPB Rules\" [KB]", prompt);
        // Field 3 is plain LLM → no [KB] tag
        Assert.DoesNotContain("\"Empathy\" [KB]", prompt);
    }

    // ── Description is included in the prompt ─────────────────────────────────

    [Fact]
    public void BuildSystemPrompt_FieldWithDescription_IncludesDescriptionInPrompt()
    {
        var fields = new List<AutoAuditFieldDefinition>
        {
            Field(1, "CFPB Regulatory Compliance", "Compliance", max: 1, type: "LLM",
                desc: "Adheres to all CFPB regulations including fair lending, UDAAP, and debt collection rules")
        };

        var prompt = AzureOpenAIAutoAuditService.BuildSystemPrompt("Test Form", fields, null);

        Assert.Contains("CFPB regulations", prompt);
    }

    // ── LLM fallback rule is always present ───────────────────────────────────

    [Fact]
    public void BuildSystemPrompt_AlwaysIncludesLlmFallbackRule()
    {
        var fields = new List<AutoAuditFieldDefinition> { Field(1, "Greeting", "Opening") };

        var prompt = AzureOpenAIAutoAuditService.BuildSystemPrompt("Test Form", fields, null);

        Assert.Contains("regulatory standards", prompt);
        Assert.Contains("general QA industry knowledge", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_WithKbContext_IncludesBothKbAndFallbackRules()
    {
        var fields = new List<AutoAuditFieldDefinition>
        {
            Field(1, "PCI DSS", "Compliance", type: "KnowledgeBased"),
            Field(2, "Greeting", "Opening")
        };
        var kbMap = new Dictionary<int, string> { [1] = "PCI context" };

        var prompt = AzureOpenAIAutoAuditService.BuildSystemPrompt("Test Form", fields, kbMap);

        Assert.Contains("Fields marked [KB] must be scored against the provided Knowledge Base", prompt);
        Assert.Contains("Fields without a [KB] tag should be scored using general QA industry knowledge", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_NoKbContext_KbRuleOmitted()
    {
        var fields = new List<AutoAuditFieldDefinition> { Field(1, "CFPB", "Compliance", type: "KnowledgeBased") };

        var prompt = AzureOpenAIAutoAuditService.BuildSystemPrompt("Test Form", fields, new Dictionary<int, string>());

        Assert.DoesNotContain("[KB]", prompt);
    }
}
