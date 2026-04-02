using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QAAutomation.API.Data;
using QAAutomation.API.DTOs;
using QAAutomation.API.Models;
using Xunit;

namespace QAAutomation.Tests;

/// <summary>
/// Integration tests for the end-to-end call pipeline.
/// Tests cover job creation (batch URL and connector), project scoping, processing,
/// and automatic result persistence.
/// </summary>
public class CallPipelineTests : IClassFixture<PipelineTestFactory>
{
    private readonly HttpClient _client;
    private readonly PipelineTestFactory _factory;

    public CallPipelineTests(PipelineTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<int> CreateProjectAsync(string name)
    {
        var resp = await _client.PostAsJsonAsync("/api/projects", new { name, description = name, isActive = true });
        resp.EnsureSuccessStatusCode();
        var p = await resp.Content.ReadFromJsonAsync<ProjectDto>();
        return p!.Id;
    }

    private async Task<EvaluationFormDto> CreateFormWithLobAsync(int projectId, string formName)
    {
        var lobResp = await _client.PostAsJsonAsync("/api/lobs",
            new { projectId, name = formName + " LOB", isActive = true });
        lobResp.EnsureSuccessStatusCode();
        var lob = await lobResp.Content.ReadFromJsonAsync<LobDto>();

        var formResp = await _client.PostAsJsonAsync("/api/evaluationforms", new CreateEvaluationFormDto
        {
            Name = formName,
            LobId = lob!.Id,
            Sections = new List<CreateFormSectionDto>
            {
                new()
                {
                    Title = "General", Order = 1,
                    Fields = new List<CreateFormFieldDto>
                    {
                        new() { Label = "Overall", FieldType = FieldType.Rating, MaxRating = 5, IsRequired = true, Order = 1 }
                    }
                }
            }
        });
        formResp.EnsureSuccessStatusCode();
        return (await formResp.Content.ReadFromJsonAsync<EvaluationFormDto>())!;
    }

    // ── Create batch-URL job ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateBatchUrlJob_Returns201WithCorrectItemCount()
    {
        int projectId = await CreateProjectAsync("Pipeline-BatchUrl-Project");
        var form = await CreateFormWithLobAsync(projectId, "BatchUrl-Form");

        var dto = new
        {
            name = "Test Batch Job",
            formId = form.Id,
            projectId,
            submittedBy = "test-user",
            items = new[]
            {
                new { url = "https://example.com/call1.txt", agentName = "Alice", callReference = "REF-001" },
                new { url = "https://example.com/call2.txt", agentName = "Bob",   callReference = "REF-002" }
            }
        };

        var resp = await _client.PostAsJsonAsync("/api/callpipeline/batch-urls", dto);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var job = await resp.Content.ReadFromJsonAsync<CallPipelineJobDto>();
        Assert.NotNull(job);
        Assert.Equal("BatchUrl", job!.SourceType);
        Assert.Equal("Test Batch Job", job.Name);
        Assert.Equal("Pending", job.Status);
        Assert.Equal(2, job.TotalItems);
        Assert.Equal(2, job.Items.Count);
        Assert.All(job.Items, i => Assert.Equal("Pending", i.Status));
    }

    // ── Empty item list is rejected ───────────────────────────────────────────

    [Fact]
    public async Task CreateBatchUrlJob_EmptyItems_Returns400()
    {
        int projectId = await CreateProjectAsync("Pipeline-Empty-Project");
        var form = await CreateFormWithLobAsync(projectId, "Empty-Form");

        var dto = new
        {
            name = "Empty Job",
            formId = form.Id,
            projectId,
            submittedBy = "test-user",
            items = Array.Empty<object>()
        };

        var resp = await _client.PostAsJsonAsync("/api/callpipeline/batch-urls", dto);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ── Invalid connector type is rejected ────────────────────────────────────

    [Fact]
    public async Task CreateConnectorJob_InvalidSourceType_Returns400()
    {
        int projectId = await CreateProjectAsync("Pipeline-Connector-Invalid-Project");
        var form = await CreateFormWithLobAsync(projectId, "Connector-Invalid-Form");

        var dto = new
        {
            name = "Bad Connector Job",
            sourceType = "InvalidType",
            formId = form.Id,
            projectId,
            submittedBy = "test-user"
        };

        var resp = await _client.PostAsJsonAsync("/api/callpipeline/from-connector", dto);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ── Project scoping ───────────────────────────────────────────────────────

    [Fact]
    public async Task PipelineJobs_FilteredByProject_DoNotLeakAcrossProjects()
    {
        int projectA = await CreateProjectAsync("Pipeline-Scope-ProjectA");
        int projectB = await CreateProjectAsync("Pipeline-Scope-ProjectB");
        var formA = await CreateFormWithLobAsync(projectA, "Scope-Form-A");
        var formB = await CreateFormWithLobAsync(projectB, "Scope-Form-B");

        // Create one job per project
        await _client.PostAsJsonAsync("/api/callpipeline/batch-urls", new
        {
            name = "Job for A",
            formId = formA.Id,
            projectId = projectA,
            submittedBy = "test",
            items = new[] { new { url = "https://example.com/a.txt" } }
        });
        await _client.PostAsJsonAsync("/api/callpipeline/batch-urls", new
        {
            name = "Job for B",
            formId = formB.Id,
            projectId = projectB,
            submittedBy = "test",
            items = new[] { new { url = "https://example.com/b.txt" } }
        });

        var jobsA = await _client.GetFromJsonAsync<List<CallPipelineJobDto>>($"/api/callpipeline?projectId={projectA}");
        Assert.NotNull(jobsA);
        Assert.Contains(jobsA!, j => j.Name == "Job for A");
        Assert.DoesNotContain(jobsA!, j => j.Name == "Job for B");

        var jobsB = await _client.GetFromJsonAsync<List<CallPipelineJobDto>>($"/api/callpipeline?projectId={projectB}");
        Assert.NotNull(jobsB);
        Assert.Contains(jobsB!, j => j.Name == "Job for B");
        Assert.DoesNotContain(jobsB!, j => j.Name == "Job for A");
    }

    // ── Get single job ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPipelineJob_ReturnsJobWithItems()
    {
        int projectId = await CreateProjectAsync("Pipeline-GetById-Project");
        var form = await CreateFormWithLobAsync(projectId, "GetById-Form");

        var createResp = await _client.PostAsJsonAsync("/api/callpipeline/batch-urls", new
        {
            name = "GetById Test Job",
            formId = form.Id,
            projectId,
            submittedBy = "test",
            items = new[]
            {
                new { url = "https://example.com/c.txt", agentName = "Charlie" }
            }
        });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<CallPipelineJobDto>();

        var fetched = await _client.GetFromJsonAsync<CallPipelineJobDto>($"/api/callpipeline/{created!.Id}");
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched!.Id);
        Assert.Equal("GetById Test Job", fetched.Name);
        Assert.Single(fetched.Items);
        Assert.Equal("Charlie", fetched.Items[0].AgentName);
    }

    // ── Process with a real transcript (via mock HTTP server) ─────────────────

    [Fact]
    public async Task ProcessJob_WithValidTranscriptUrl_CreatesEvaluationResult()
    {
        int projectId = await CreateProjectAsync("Pipeline-Process-Project");
        var form = await CreateFormWithLobAsync(projectId, "Process-Form");

        // The factory starts a local HTTP echo server at a configurable port
        var transcriptUrl = _factory.TranscriptServerUrl + "/transcript/test.txt";

        var createResp = await _client.PostAsJsonAsync("/api/callpipeline/batch-urls", new
        {
            name = "Process Test Job",
            formId = form.Id,
            projectId,
            submittedBy = "test",
            items = new[]
            {
                new { url = transcriptUrl, agentName = "Diana", callReference = "PROC-001" }
            }
        });
        createResp.EnsureSuccessStatusCode();
        var job = await createResp.Content.ReadFromJsonAsync<CallPipelineJobDto>();

        // Trigger processing (inline for ≤ 5 items)
        var processResp = await _client.PostAsync($"/api/callpipeline/{job!.Id}/process", null);
        // Should be 200 OK (or 202 accepted for large batches)
        Assert.True(processResp.StatusCode == HttpStatusCode.OK || processResp.StatusCode == HttpStatusCode.Accepted);

        // Retrieve updated job
        var updated = await _client.GetFromJsonAsync<CallPipelineJobDto>($"/api/callpipeline/{job.Id}");
        Assert.NotNull(updated);
        Assert.Equal("Completed", updated!.Status);
        Assert.Single(updated.Items);
        Assert.Equal("Completed", updated.Items[0].Status);
        Assert.NotNull(updated.Items[0].EvaluationResultId);
        Assert.True(updated.Items[0].ScorePercent >= 0);
    }

    // ── Re-processing a running job returns 409 ───────────────────────────────

    [Fact]
    public async Task ProcessJob_AlreadyRunning_Returns409()
    {
        int projectId = await CreateProjectAsync("Pipeline-Running-Project");
        var form = await CreateFormWithLobAsync(projectId, "Running-Form");

        var createResp = await _client.PostAsJsonAsync("/api/callpipeline/batch-urls", new
        {
            name = "Running Test Job",
            formId = form.Id,
            projectId,
            submittedBy = "test",
            items = new[] { new { url = "https://example.com/d.txt" } }
        });
        createResp.EnsureSuccessStatusCode();
        var job = await createResp.Content.ReadFromJsonAsync<CallPipelineJobDto>();

        // Manually set status to Running in DB to simulate in-flight job
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbJob = await db.CallPipelineJobs.FindAsync(job!.Id);
        dbJob!.Status = "Running";
        await db.SaveChangesAsync();

        var resp = await _client.PostAsync($"/api/callpipeline/{job.Id}/process", null);
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    // ── Resume endpoint resets stalled Running job ────────────────────────────

    [Fact]
    public async Task ResumeJob_StalledRunning_ResetsStatusToPending()
    {
        int projectId = await CreateProjectAsync("Pipeline-Resume-Project");
        var form = await CreateFormWithLobAsync(projectId, "Resume-Form");

        var createResp = await _client.PostAsJsonAsync("/api/callpipeline/batch-urls", new
        {
            name = "Stalled Resume Test Job",
            formId = form.Id,
            projectId,
            submittedBy = "test",
            items = new[] { new { url = "https://example.com/stalled.txt" } }
        });
        createResp.EnsureSuccessStatusCode();
        var job = await createResp.Content.ReadFromJsonAsync<CallPipelineJobDto>();

        // Simulate the job being stuck in Running state (e.g. app was restarted)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbJob = await db.CallPipelineJobs
                .Include(j => j.Items)
                .FirstAsync(j => j.Id == job!.Id);
            dbJob.Status = "Running";
            // Simulate an item stuck in Processing state
            foreach (var item in dbJob.Items)
                item.Status = "Processing";
            await db.SaveChangesAsync();
        }

        // The process endpoint should still return 409 (it only handles Pending/Failed)
        var processResp = await _client.PostAsync($"/api/callpipeline/{job!.Id}/process", null);
        Assert.Equal(HttpStatusCode.Conflict, processResp.StatusCode);

        // The resume endpoint should succeed and reset the job
        var resumeResp = await _client.PostAsync($"/api/callpipeline/{job.Id}/resume", null);
        Assert.Equal(HttpStatusCode.OK, resumeResp.StatusCode);

        // Brief wait to let the fire-and-forget processing attempt complete
        // (the mock URL will fail quickly, moving the job to a terminal state)
        await Task.Delay(500);

        // Verify the job is no longer permanently stuck — it must have moved out of
        // the deadlocked "Running + all items Processing" state
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbJob = await db.CallPipelineJobs
                .Include(j => j.Items)
                .FirstAsync(j => j.Id == job.Id);

            // The job must NOT be stuck: it should be Completed, Failed, or at most Running
            // with items that are NOT all still "Processing" (i.e. real progress happened)
            var allStillProcessing = dbJob.Items.All(i => i.Status == "Processing");
            Assert.False(
                dbJob.Status == "Running" && allStillProcessing,
                $"Job should not be permanently stuck in Running+Processing state. Status={dbJob.Status}");
        }
    }

    [Fact]
    public async Task ResumeJob_NotRunning_Returns409()
    {
        int projectId = await CreateProjectAsync("Pipeline-ResumeNotRunning-Project");
        var form = await CreateFormWithLobAsync(projectId, "ResumeNotRunning-Form");

        var createResp = await _client.PostAsJsonAsync("/api/callpipeline/batch-urls", new
        {
            name = "Pending Job — Resume Should Fail",
            formId = form.Id,
            projectId,
            submittedBy = "test",
            items = new[] { new { url = "https://example.com/pending.txt" } }
        });
        createResp.EnsureSuccessStatusCode();
        var job = await createResp.Content.ReadFromJsonAsync<CallPipelineJobDto>();
        // Job is Pending, not Running — resume should return 409
        var resp = await _client.PostAsync($"/api/callpipeline/{job!.Id}/resume", null);
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task ResumeJob_NotFound_Returns404()
    {
        var resp = await _client.PostAsync("/api/callpipeline/999999/resume", null);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}

/// <summary>
/// Test factory that starts a minimal embedded transcript server so the pipeline
/// can fetch real text content during integration tests, without hitting the internet.
/// </summary>
public class PipelineTestFactory : TestWebApplicationFactory, IAsyncDisposable
{
    private readonly int _transcriptPort;
    private readonly System.Net.HttpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _serverTask;

    public string TranscriptServerUrl { get; }

    public PipelineTestFactory()
    {
        // Pick a random free port
        _transcriptPort = FreeTcpPort();
        TranscriptServerUrl = $"http://localhost:{_transcriptPort}";

        _listener = new System.Net.HttpListener();
        _listener.Prefixes.Add($"{TranscriptServerUrl}/");
        _listener.Start();

        _serverTask = ServeAsync(_cts.Token);
    }

    private static int FreeTcpPort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private async Task ServeAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            System.Net.HttpListenerContext? ctx = null;
            try { ctx = await _listener.GetContextAsync(); }
            catch { break; }

            const string transcript = """
                Agent: Thank you for calling customer support. My name is Diana, how can I help you today?
                Customer: Hi, I need help with my account billing.
                Agent: Of course! Let me pull up your account. Can I get your account number?
                Customer: Sure, it's 12345.
                Agent: I see the issue — there was a duplicate charge. I'll refund that now.
                Customer: Thank you so much!
                Agent: Is there anything else I can help you with today?
                Customer: No, that's all. Thank you!
                Agent: Thank you for calling. Have a great day!
                """;

            var bytes = System.Text.Encoding.UTF8.GetBytes(transcript);
            ctx.Response.ContentType = "text/plain; charset=utf-8";
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.StatusCode = 200;
            await ctx.Response.OutputStream.WriteAsync(bytes, ct);
            ctx.Response.Close();
        }
    }

    public new async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _listener.Stop();
        try { await _serverTask; } catch { /* expected */ }
        _cts.Dispose();
        Dispose();
    }
}
