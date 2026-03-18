using System.Net;
using System.Net.Http.Json;
using QAAutomation.API.DTOs;
using Xunit;

namespace QAAutomation.Tests;

/// <summary>
/// Integration tests for the Training Need Identification (TNI) module:
///   • TrainingPlansController — CRUD, status transitions, close-loop, item completion
/// </summary>
public class TrainingPlanTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TrainingPlanTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static object DefaultPlanDto(string title = "Test Plan") => new
    {
        title,
        description = "Test description",
        agentName = "Sarah Mitchell",
        agentUsername = "sarah.mitchell",
        trainerName = "John Trainer",
        trainerUsername = "john.trainer",
        dueDate = (DateTime?)null,
        projectId = (int?)null,
        evaluationResultId = (int?)null,
        humanReviewItemId = (int?)null,
        createdBy = "qm_user",
        items = new[]
        {
            new { targetArea = "Compliance", itemType = "Observation", content = "Failed CID verification", order = 0 },
            new { targetArea = "Compliance", itemType = "Recommendation", content = "Role-play CID verification", order = 1 }
        }
    };

    private async Task<TrainingPlanDto> CreatePlanAsync(string title = "Test Plan")
    {
        var r = await _client.PostAsJsonAsync("/api/trainingplans", DefaultPlanDto(title));
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<TrainingPlanDto>())!;
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePlan_ValidDto_Returns201WithItems()
    {
        var r = await _client.PostAsJsonAsync("/api/trainingplans", DefaultPlanDto("Create Test"));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var plan = await r.Content.ReadFromJsonAsync<TrainingPlanDto>();
        Assert.NotNull(plan);
        Assert.Equal("Create Test", plan!.Title);
        Assert.Equal("Draft", plan.Status);
        Assert.Equal(2, plan.TotalItems);
        Assert.Equal(0, plan.CompletedItems);
        Assert.Equal("sarah.mitchell", plan.AgentUsername);
        Assert.Equal("john.trainer", plan.TrainerUsername);
        Assert.Equal("qm_user", plan.CreatedBy);
        Assert.All(plan.Items, item => Assert.Equal("Pending", item.Status));
    }

    [Fact]
    public async Task CreatePlan_NoItems_Returns400()
    {
        var dto = new
        {
            title = "Empty Plan",
            agentName = "Agent A",
            trainerName = "Trainer B",
            createdBy = "qm",
            items = Array.Empty<object>()
        };
        var r = await _client.PostAsJsonAsync("/api/trainingplans", dto);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task GetById_ExistingPlan_ReturnsPlan()
    {
        var created = await CreatePlanAsync("GetById Test");
        var r = await _client.GetFromJsonAsync<TrainingPlanDto>($"/api/trainingplans/{created.Id}");
        Assert.NotNull(r);
        Assert.Equal(created.Id, r!.Id);
    }

    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        var r = await _client.GetAsync("/api/trainingplans/999999");
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    [Fact]
    public async Task GetAll_FilterByStatus_ReturnsOnlyMatchingPlans()
    {
        await CreatePlanAsync("Status Filter Test");
        var r = await _client.GetFromJsonAsync<List<TrainingPlanDto>>("/api/trainingplans?status=Draft");
        Assert.NotNull(r);
        Assert.All(r!, p => Assert.Equal("Draft", p.Status));
    }

    [Fact]
    public async Task GetAll_FilterByAgentUsername_ReturnsMatchingPlans()
    {
        await CreatePlanAsync("Agent Filter Test");
        var r = await _client.GetFromJsonAsync<List<TrainingPlanDto>>("/api/trainingplans?agentUsername=sarah.mitchell");
        Assert.NotNull(r);
        Assert.All(r!, p => Assert.Equal("sarah.mitchell", p.AgentUsername));
    }

    [Fact]
    public async Task UpdatePlan_ValidDto_UpdatesFields()
    {
        var plan = await CreatePlanAsync("Update Test");
        var dto = new
        {
            title = "Updated Title",
            description = "Updated desc",
            agentName = "Agent Updated",
            agentUsername = "agent.updated",
            trainerName = "Trainer Updated",
            trainerUsername = "trainer.updated",
            dueDate = DateTime.UtcNow.AddDays(30)
        };
        var r = await _client.PutAsJsonAsync($"/api/trainingplans/{plan.Id}", dto);
        Assert.True(r.IsSuccessStatusCode);
        var updated = await r.Content.ReadFromJsonAsync<TrainingPlanDto>();
        Assert.Equal("Updated Title", updated!.Title);
        Assert.Equal("Agent Updated", updated.AgentName);
    }

    [Fact]
    public async Task DeletePlan_Returns204()
    {
        var plan = await CreatePlanAsync("Delete Test");
        var delR = await _client.DeleteAsync($"/api/trainingplans/{plan.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delR.StatusCode);
        var getR = await _client.GetAsync($"/api/trainingplans/{plan.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getR.StatusCode);
    }

    // ── Status transitions ────────────────────────────────────────────────────

    [Fact]
    public async Task Activate_DraftPlan_BecomesActive()
    {
        var plan = await CreatePlanAsync("Activate Test");
        Assert.Equal("Draft", plan.Status);

        var r = await _client.PutAsJsonAsync($"/api/trainingplans/{plan.Id}/status",
            new { status = "Active", updatedBy = "qm" });
        Assert.True(r.IsSuccessStatusCode);
        var updated = await r.Content.ReadFromJsonAsync<TrainingPlanDto>();
        Assert.Equal("Active", updated!.Status);
    }

    [Fact]
    public async Task StartTraining_ActivePlan_BecomesInProgress()
    {
        var plan = await CreatePlanAsync("Start Test");
        // Draft → Active → InProgress
        await _client.PutAsJsonAsync($"/api/trainingplans/{plan.Id}/status",
            new { status = "Active", updatedBy = "qm" });
        var r = await _client.PutAsJsonAsync($"/api/trainingplans/{plan.Id}/status",
            new { status = "InProgress", updatedBy = "trainer" });
        Assert.True(r.IsSuccessStatusCode);
        var updated = await r.Content.ReadFromJsonAsync<TrainingPlanDto>();
        Assert.Equal("InProgress", updated!.Status);
    }

    [Fact]
    public async Task UpdateStatus_InvalidStatus_Returns400()
    {
        var plan = await CreatePlanAsync("Invalid Status Test");
        var r = await _client.PutAsJsonAsync($"/api/trainingplans/{plan.Id}/status",
            new { status = "FLYING", updatedBy = "qm" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task CloseDraftPlan_Returns400()
    {
        var plan = await CreatePlanAsync("Close Draft Test");
        Assert.Equal("Draft", plan.Status);

        var r = await _client.PostAsJsonAsync($"/api/trainingplans/{plan.Id}/close",
            new { closedBy = "qm", closingNotes = "closing early" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    // ── Item completion ───────────────────────────────────────────────────────

    [Fact]
    public async Task CompleteItem_MarksItemDone()
    {
        var plan = await CreatePlanAsync("Complete Item Test");
        // Activate + Start
        await _client.PutAsJsonAsync($"/api/trainingplans/{plan.Id}/status",
            new { status = "Active", updatedBy = "qm" });
        await _client.PutAsJsonAsync($"/api/trainingplans/{plan.Id}/status",
            new { status = "InProgress", updatedBy = "trainer" });

        var firstItem = plan.Items.OrderBy(i => i.Order).First();
        var r = await _client.PutAsJsonAsync(
            $"/api/trainingplans/{plan.Id}/items/{firstItem.Id}/complete",
            new { completedBy = "john.trainer", completionNotes = "Done in session 1." });
        Assert.True(r.IsSuccessStatusCode);
        var item = await r.Content.ReadFromJsonAsync<TrainingPlanItemDto>();
        Assert.Equal("Done", item!.Status);
        Assert.Equal("john.trainer", item.CompletedBy);
        Assert.NotNull(item.CompletedAt);
    }

    [Fact]
    public async Task CompleteAllItems_AutoAdvancesToCompleted()
    {
        var plan = await CreatePlanAsync("AutoComplete Test");
        // Advance to InProgress
        await _client.PutAsJsonAsync($"/api/trainingplans/{plan.Id}/status",
            new { status = "Active", updatedBy = "qm" });
        await _client.PutAsJsonAsync($"/api/trainingplans/{plan.Id}/status",
            new { status = "InProgress", updatedBy = "trainer" });

        // Mark all items done
        foreach (var item in plan.Items)
        {
            await _client.PutAsJsonAsync(
                $"/api/trainingplans/{plan.Id}/items/{item.Id}/complete",
                new { completedBy = "john.trainer", completionNotes = "" });
        }

        // Plan should auto-advance to Completed
        var refreshed = await _client.GetFromJsonAsync<TrainingPlanDto>($"/api/trainingplans/{plan.Id}");
        Assert.Equal("Completed", refreshed!.Status);
        Assert.Equal(refreshed.TotalItems, refreshed.CompletedItems);
    }

    // ── Close loop ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ClosePlan_InProgressPlan_SucceedsAndRecordsClosedBy()
    {
        var plan = await CreatePlanAsync("Close Loop Test");
        await _client.PutAsJsonAsync($"/api/trainingplans/{plan.Id}/status",
            new { status = "Active", updatedBy = "qm" });
        await _client.PutAsJsonAsync($"/api/trainingplans/{plan.Id}/status",
            new { status = "InProgress", updatedBy = "trainer" });

        var r = await _client.PostAsJsonAsync($"/api/trainingplans/{plan.Id}/close",
            new { closedBy = "quality_manager", closingNotes = "Training confirmed complete. Loop closed." });
        Assert.True(r.IsSuccessStatusCode);
        var closed = await r.Content.ReadFromJsonAsync<TrainingPlanDto>();
        Assert.Equal("Closed", closed!.Status);
        Assert.Equal("quality_manager", closed.ClosedBy);
        Assert.NotNull(closed.ClosedAt);
        Assert.Contains("Loop closed", closed.ClosingNotes!);
    }

    [Fact]
    public async Task CloseAlreadyClosedPlan_Returns400()
    {
        var plan = await CreatePlanAsync("Double Close Test");
        await _client.PutAsJsonAsync($"/api/trainingplans/{plan.Id}/status",
            new { status = "Active", updatedBy = "qm" });
        await _client.PutAsJsonAsync($"/api/trainingplans/{plan.Id}/status",
            new { status = "InProgress", updatedBy = "trainer" });
        await _client.PostAsJsonAsync($"/api/trainingplans/{plan.Id}/close",
            new { closedBy = "qm", closingNotes = "" });

        var r = await _client.PostAsJsonAsync($"/api/trainingplans/{plan.Id}/close",
            new { closedBy = "qm", closingNotes = "trying again" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task UpdateClosedPlan_Returns400()
    {
        var plan = await CreatePlanAsync("Update Closed Test");
        await _client.PutAsJsonAsync($"/api/trainingplans/{plan.Id}/status",
            new { status = "Active", updatedBy = "qm" });
        await _client.PutAsJsonAsync($"/api/trainingplans/{plan.Id}/status",
            new { status = "InProgress", updatedBy = "trainer" });
        await _client.PostAsJsonAsync($"/api/trainingplans/{plan.Id}/close",
            new { closedBy = "qm", closingNotes = "" });

        var dto = new { title = "Attempt Update", agentName = "X", trainerName = "Y", description = "" };
        var r = await _client.PutAsJsonAsync($"/api/trainingplans/{plan.Id}", dto);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }
}
