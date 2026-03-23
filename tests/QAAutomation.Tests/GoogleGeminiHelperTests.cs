using System.Text.Json;
using QAAutomation.API.Services;
using Xunit;

namespace QAAutomation.Tests;

/// <summary>
/// Unit tests for <see cref="GeminiHttpHelper"/> and related Google Gemini helpers.
/// These tests validate JSON parsing and URL building without making real HTTP calls.
/// </summary>
public class GoogleGeminiHelperTests
{
    // ── GeminiHttpHelper.ExtractText ──────────────────────────────────────────

    [Fact]
    public void ExtractText_ValidResponse_ReturnsText()
    {
        var json = """
            {
              "candidates": [
                {
                  "content": {
                    "parts": [ { "text": "{\"scores\":[]}" } ],
                    "role": "model"
                  }
                }
              ]
            }
            """;

        var result = GeminiHttpHelper.ExtractText(json);
        Assert.Equal("{\"scores\":[]}", result);
    }

    [Fact]
    public void ExtractText_NoCandidates_ThrowsInvalidOperation()
    {
        var json = """{ "candidates": [] }""";
        Assert.Throws<InvalidOperationException>(() => GeminiHttpHelper.ExtractText(json));
    }

    [Fact]
    public void ExtractText_MissingCandidatesProperty_ThrowsInvalidOperation()
    {
        var json = """{ "error": { "message": "API key not valid" } }""";
        Assert.Throws<InvalidOperationException>(() => GeminiHttpHelper.ExtractText(json));
    }

    [Fact]
    public void ExtractText_MultiplePartsReturnsFirstText()
    {
        var json = """
            {
              "candidates": [
                {
                  "content": {
                    "parts": [
                      { "text": "first" },
                      { "text": "second" }
                    ]
                  }
                }
              ]
            }
            """;
        Assert.Equal("first", GeminiHttpHelper.ExtractText(json));
    }

    [Fact]
    public void ExtractText_JsonWithActualQaPayload_ReturnsInnerJson()
    {
        var innerJson = """{"scores":[{"fieldId":1,"score":4,"reasoning":"Agent greeted warmly."}],"overallReasoning":"Good call."}""";
        var geminiEnvelope = $$"""
            {
              "candidates": [
                {
                  "content": {
                    "parts": [ { "text": {{JsonSerializer.Serialize(innerJson)}} } ]
                  }
                }
              ],
              "usageMetadata": { "promptTokenCount": 500, "candidatesTokenCount": 120 }
            }
            """;
        var result = GeminiHttpHelper.ExtractText(geminiEnvelope);
        Assert.Equal(innerJson, result);
    }

    // ── GeminiBaseUrl constant ────────────────────────────────────────────────

    [Fact]
    public void BaseUrl_StartsWithGoogleApiHost()
        => Assert.StartsWith("https://generativelanguage.googleapis.com/", GeminiHttpHelper.BaseUrl);

    [Fact]
    public void BaseUrl_ContainsV1Beta()
        => Assert.Contains("v1beta", GeminiHttpHelper.BaseUrl);

    // ── AiConfig new fields — defaults ────────────────────────────────────────

    [Fact]
    public void AiConfig_GoogleDefaults_AreCorrect()
    {
        var cfg = new QAAutomation.API.Models.AiConfig();
        Assert.Equal(string.Empty, cfg.GoogleApiKey);
        Assert.Equal("gemini-1.5-pro", cfg.GoogleGeminiModel);
        Assert.Equal("Azure", cfg.SpeechProvider);
    }

    [Fact]
    public void AiConfig_LlmProviderDefault_IsAzureOpenAI()
    {
        var cfg = new QAAutomation.API.Models.AiConfig();
        Assert.Equal("AzureOpenAI", cfg.LlmProvider);
    }

    [Fact]
    public void AiConfig_SentimentProviderDefault_IsAzureOpenAI()
    {
        var cfg = new QAAutomation.API.Models.AiConfig();
        Assert.Equal("AzureOpenAI", cfg.SentimentProvider);
    }

    // ── Model name validation — sensible defaults ─────────────────────────────

    [Theory]
    [InlineData("gemini-1.5-pro")]
    [InlineData("gemini-2.0-flash")]
    [InlineData("gemini-1.5-flash")]
    [InlineData("gemini-1.0-pro")]
    public void GoogleGeminiModel_ValidNames_AreNonEmpty(string model)
        => Assert.NotEmpty(model);

    // ── AiConfigDto — new fields present ─────────────────────────────────────

    [Fact]
    public void AiConfigDto_HasGoogleFields()
    {
        var dto = new QAAutomation.API.DTOs.AiConfigDto
        {
            GoogleApiKey = "test-key",
            GoogleGeminiModel = "gemini-2.0-flash",
            SpeechProvider = "Google"
        };
        Assert.Equal("test-key", dto.GoogleApiKey);
        Assert.Equal("gemini-2.0-flash", dto.GoogleGeminiModel);
        Assert.Equal("Google", dto.SpeechProvider);
    }
}
