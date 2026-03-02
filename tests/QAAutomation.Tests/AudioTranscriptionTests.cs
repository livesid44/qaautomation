using System.Net;
using System.Net.Http.Json;
using QAAutomation.API.DTOs;
using QAAutomation.API.Models;
using QAAutomation.API.Services;
using Xunit;

namespace QAAutomation.Tests;

/// <summary>
/// Unit tests for AudioFormatHelper — validates audio detection by URL extension
/// and HTTP Content-Type.
/// </summary>
public class AudioFormatHelperTests
{
    // ── IsAudioUrl ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("https://storage.example.com/calls/recording.mp3",  true)]
    [InlineData("https://storage.example.com/calls/recording.wav",  true)]
    [InlineData("https://storage.example.com/calls/recording.ogg",  true)]
    [InlineData("https://storage.example.com/calls/recording.flac", true)]
    [InlineData("https://storage.example.com/calls/recording.m4a",  true)]
    [InlineData("https://storage.example.com/calls/recording.mp4",  true)]
    [InlineData("https://storage.example.com/calls/recording.wma",  true)]
    [InlineData("https://storage.example.com/calls/recording.aac",  true)]
    [InlineData("https://storage.example.com/calls/recording.opus", true)]
    [InlineData("https://storage.example.com/calls/transcript.txt", false)]
    [InlineData("https://storage.example.com/calls/transcript.json",false)]
    [InlineData("https://storage.example.com/calls/transcript.pdf", false)]
    [InlineData("https://api.verint.com/v1/calls/1234/transcript",  false)]
    [InlineData("",                                                  false)]
    public void IsAudioUrl_ReturnsCorrectResult(string url, bool expected)
    {
        Assert.Equal(expected, AudioFormatHelper.IsAudioUrl(url));
    }

    [Fact]
    public void IsAudioUrl_WithQueryString_StillDetectsExtension()
    {
        // SAS URLs have long query strings after the audio extension
        Assert.True(AudioFormatHelper.IsAudioUrl(
            "https://mystorage.blob.core.windows.net/calls/rec.mp3?sv=2023&se=...&sig=abc"));
    }

    [Fact]
    public void IsAudioUrl_CaseInsensitive()
    {
        Assert.True(AudioFormatHelper.IsAudioUrl("https://example.com/recording.MP3"));
        Assert.True(AudioFormatHelper.IsAudioUrl("https://example.com/recording.WAV"));
    }

    // ── IsAudioContentType ────────────────────────────────────────────────────

    [Theory]
    [InlineData("audio/mpeg",          true)]
    [InlineData("audio/wav",           true)]
    [InlineData("audio/ogg",           true)]
    [InlineData("audio/flac",          true)]
    [InlineData("audio/mp4",           true)]
    [InlineData("audio/aac",           true)]
    [InlineData("audio/opus",          true)]
    [InlineData("video/mp4",           true)]
    [InlineData("video/webm",          true)]
    [InlineData("text/plain",          false)]
    [InlineData("application/json",    false)]
    [InlineData("text/html",           false)]
    [InlineData(null,                  false)]
    [InlineData("",                    false)]
    public void IsAudioContentType_ReturnsCorrectResult(string? contentType, bool expected)
    {
        Assert.Equal(expected, AudioFormatHelper.IsAudioContentType(contentType));
    }

    [Fact]
    public void IsAudioContentType_StripsParameters()
    {
        // Content-Type: audio/wav; codec=pcm
        Assert.True(AudioFormatHelper.IsAudioContentType("audio/wav; codec=pcm"));
    }

    [Fact]
    public void IsAudioContentType_AnyAudioSubtype_IsTrue()
    {
        // Unknown audio subtypes should still be detected
        Assert.True(AudioFormatHelper.IsAudioContentType("audio/x-custom-format"));
    }

    // ── IsAudio (combined) ────────────────────────────────────────────────────

    [Fact]
    public void IsAudio_AudioUrl_ReturnsTrue()
    {
        Assert.True(AudioFormatHelper.IsAudio("https://example.com/call.mp3"));
    }

    [Fact]
    public void IsAudio_TextUrlWithAudioContentType_ReturnsTrue()
    {
        // URL looks like a text file but server returns audio content-type
        Assert.True(AudioFormatHelper.IsAudio(
            "https://example.com/api/download/recording",
            contentType: "audio/mpeg"));
    }

    [Fact]
    public void IsAudio_TextUrl_NullContentType_ReturnsFalse()
    {
        Assert.False(AudioFormatHelper.IsAudio("https://example.com/transcript.txt", null));
    }
}

/// <summary>
/// Unit tests for AzureSpeechService.ExtractDisplayText — validates transcript extraction
/// from Azure Speech batch result JSON (no real API calls).
/// </summary>
public class AzureSpeechServiceExtractTests
{
    [Fact]
    public void ExtractDisplayText_CombinedPhrases_ReturnsFullText()
    {
        var json = """
            {
              "combinedRecognizedPhrases": [
                { "display": "Hello this is a test." },
                { "display": "How can I help you today?" }
              ]
            }
            """;
        var result = AzureSpeechService.ExtractDisplayText(json);
        Assert.Equal("Hello this is a test. How can I help you today?", result);
    }

    [Fact]
    public void ExtractDisplayText_NoBestPhrases_StitchesText()
    {
        var json = """
            {
              "recognizedPhrases": [
                { "nBest": [ { "display": "Agent: Hello." } ] },
                { "nBest": [ { "display": "Customer: Hi!" } ] }
              ]
            }
            """;
        var result = AzureSpeechService.ExtractDisplayText(json);
        Assert.Equal("Agent: Hello. Customer: Hi!", result);
    }

    [Fact]
    public void ExtractDisplayText_EmptyJson_ReturnsNull()
    {
        var result = AzureSpeechService.ExtractDisplayText("{}");
        Assert.Null(result);
    }

    [Fact]
    public void ExtractDisplayText_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(AzureSpeechService.ExtractDisplayText(null!));
        Assert.Null(AzureSpeechService.ExtractDisplayText(""));
    }

    [Fact]
    public void ExtractDisplayText_PrefersCombinedOverNBest()
    {
        var json = """
            {
              "combinedRecognizedPhrases": [ { "display": "Combined text." } ],
              "recognizedPhrases": [
                { "nBest": [ { "display": "Individual phrase." } ] }
              ]
            }
            """;
        var result = AzureSpeechService.ExtractDisplayText(json);
        // Should prefer combinedRecognizedPhrases
        Assert.Equal("Combined text.", result);
    }
}

/// <summary>
/// Integration tests for the pipeline's audio handling:
/// - Audio URLs are detected correctly
/// - When Speech is not configured the item fails with a clear error message
/// </summary>
public class AudioPipelineTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public AudioPipelineTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<int> CreateProjectAsync(string name)
    {
        var r = await _client.PostAsJsonAsync("/api/projects", new { name, description = name, isActive = true });
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<ProjectDto>())!.Id;
    }

    private async Task<int> CreateFormAsync(int projectId, string name)
    {
        var lobR = await _client.PostAsJsonAsync("/api/lobs", new { projectId, name = name + " LOB", isActive = true });
        lobR.EnsureSuccessStatusCode();
        var lob = await lobR.Content.ReadFromJsonAsync<LobDto>();

        var formR = await _client.PostAsJsonAsync("/api/evaluationforms", new CreateEvaluationFormDto
        {
            Name = name,
            LobId = lob!.Id,
            Sections = new List<CreateFormSectionDto>
            {
                new() { Title = "G", Order = 1, Fields = new List<CreateFormFieldDto>
                {
                    new() { Label = "Overall", FieldType = FieldType.Rating, MaxRating = 5, IsRequired = true, Order = 1 }
                }}
            }
        });
        formR.EnsureSuccessStatusCode();
        return (await formR.Content.ReadFromJsonAsync<EvaluationFormDto>())!.Id;
    }

    /// <summary>
    /// When an audio URL is submitted and Speech is not configured (default test environment),
    /// processing the item should fail with a clear error message rather than silently succeeding
    /// or throwing an unhandled exception.
    /// </summary>
    [Fact]
    public async Task AudioUrl_WhenSpeechNotConfigured_ItemFailsWithClearError()
    {
        int pid = await CreateProjectAsync("Audio-NoSpeech-Project");
        int fid = await CreateFormAsync(pid, "Audio-NoSpeech-Form");

        // Submit an audio URL (mp3 extension triggers audio routing)
        var createResp = await _client.PostAsJsonAsync("/api/callpipeline/batch-urls", new
        {
            name = "Audio No Speech Job",
            formId = fid,
            projectId = pid,
            submittedBy = "test",
            items = new[] { new { url = "https://example.com/recording.mp3" } }
        });
        createResp.EnsureSuccessStatusCode();
        var job = await createResp.Content.ReadFromJsonAsync<CallPipelineJobDto>();

        // Trigger processing (Speech is not configured in test environment)
        var processResp = await _client.PostAsync($"/api/callpipeline/{job!.Id}/process", null);
        Assert.True(processResp.IsSuccessStatusCode);

        var updated = await _client.GetFromJsonAsync<CallPipelineJobDto>($"/api/callpipeline/{job.Id}");
        Assert.NotNull(updated);
        // The item should have failed with a clear message about Speech not being configured
        Assert.Single(updated!.Items);
        var item = updated.Items[0];
        Assert.Equal("Failed", item.Status);
        Assert.NotNull(item.ErrorMessage);
        Assert.Contains("Speech", item.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Integration tests for mixed audio + text pipeline jobs using the transcript server
/// provided by PipelineTestFactory.
/// </summary>
public class MixedAudioTextPipelineTests : IClassFixture<PipelineTestFactory>
{
    private readonly HttpClient _client;
    private readonly PipelineTestFactory _factory;

    public MixedAudioTextPipelineTests(PipelineTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<int> CreateProjectAsync(string name)
    {
        var r = await _client.PostAsJsonAsync("/api/projects", new { name, description = name, isActive = true });
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<ProjectDto>())!.Id;
    }

    private async Task<int> CreateFormAsync(int projectId, string name)
    {
        var lobR = await _client.PostAsJsonAsync("/api/lobs", new { projectId, name = name + " LOB", isActive = true });
        lobR.EnsureSuccessStatusCode();
        var lob = await lobR.Content.ReadFromJsonAsync<LobDto>();
        var formR = await _client.PostAsJsonAsync("/api/evaluationforms", new CreateEvaluationFormDto
        {
            Name = name,
            LobId = lob!.Id,
            Sections = new List<CreateFormSectionDto>
            {
                new() { Title = "G", Order = 1, Fields = new List<CreateFormFieldDto>
                {
                    new() { Label = "Overall", FieldType = FieldType.Rating, MaxRating = 5, IsRequired = true, Order = 1 }
                }}
            }
        });
        formR.EnsureSuccessStatusCode();
        return (await formR.Content.ReadFromJsonAsync<EvaluationFormDto>())!.Id;
    }

    /// <summary>
    /// A job containing one audio URL and one transcript URL:
    /// - The text item should complete successfully (scored by mock LLM)
    /// - The audio item should fail gracefully with a clear "configure Speech" error
    /// </summary>
    [Fact]
    public async Task MixedJob_AudioAndText_TextSucceedsAudioFailsGracefully()
    {
        int pid = await CreateProjectAsync("Mixed-Pipeline-Project");
        int fid = await CreateFormAsync(pid, "Mixed-Pipeline-Form");

        var transcriptUrl = _factory.TranscriptServerUrl + "/transcript/test.txt";

        var createResp = await _client.PostAsJsonAsync("/api/callpipeline/batch-urls", new
        {
            name = "Mixed Audio+Text Job",
            formId = fid,
            projectId = pid,
            submittedBy = "test",
            items = new object[]
            {
                new { url = "https://example.com/call.mp3", agentName = "Alice" },
                new { url = transcriptUrl, agentName = "Bob", callReference = "TXT-001" }
            }
        });
        createResp.EnsureSuccessStatusCode();
        var job = await createResp.Content.ReadFromJsonAsync<CallPipelineJobDto>();

        // Trigger processing (≤ 5 items → inline)
        var processResp = await _client.PostAsync($"/api/callpipeline/{job!.Id}/process", null);
        Assert.True(processResp.IsSuccessStatusCode);

        var updated = await _client.GetFromJsonAsync<CallPipelineJobDto>($"/api/callpipeline/{job.Id}");
        Assert.NotNull(updated);
        Assert.Equal(2, updated!.Items.Count);

        var audioItem = updated.Items.First(i => i.AgentName == "Alice");
        var textItem = updated.Items.First(i => i.CallReference == "TXT-001");

        // Audio item should fail with helpful message
        Assert.Equal("Failed", audioItem.Status);
        Assert.NotNull(audioItem.ErrorMessage);
        Assert.Contains("Speech", audioItem.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Text item should complete successfully
        Assert.Equal("Completed", textItem.Status);
        Assert.NotNull(textItem.EvaluationResultId);
        Assert.True(textItem.ScorePercent >= 0);
    }
}
