using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QAAutomation.API.Data;
using QAAutomation.API.DTOs;
using QAAutomation.API.Models;
using Xunit;

namespace QAAutomation.Tests;

public class EvaluationResultsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public EvaluationResultsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<EvaluationFormDto> CreateFormAsync()
    {
        var formDto = new CreateEvaluationFormDto
        {
            Name = "Result Test Form",
            Description = "Form for results testing",
            Sections = new List<CreateFormSectionDto>
            {
                new()
                {
                    Title = "Section A",
                    Order = 1,
                    Fields = new List<CreateFormFieldDto>
                    {
                        new() { Label = "Rating", FieldType = FieldType.Rating, IsRequired = true, Order = 1, MaxRating = 10 }
                    }
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/evaluationforms", formDto);
        response.EnsureSuccessStatusCode();
        var form = await response.Content.ReadFromJsonAsync<EvaluationFormDto>();
        return form!;
    }

    [Fact]
    public async Task CanSubmitEvaluationResult()
    {
        var form = await CreateFormAsync();
        var fieldId = form.Sections[0].Fields[0].Id;

        var resultDto = new CreateEvaluationResultDto
        {
            FormId = form.Id,
            EvaluatedBy = "John Doe",
            Notes = "Test evaluation",
            Scores = new List<CreateEvaluationScoreDto>
            {
                new() { FieldId = fieldId, Value = "8", NumericValue = 8.0 }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/evaluationresults", resultDto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<EvaluationResultDto>();
        Assert.NotNull(result);
        Assert.Equal(form.Id, result.FormId);
        Assert.Equal("John Doe", result.EvaluatedBy);
        Assert.Single(result.Scores);
        Assert.Equal(8.0, result.TotalScore);
    }

    [Fact]
    public async Task CanRetrieveResultsByFormId()
    {
        var form = await CreateFormAsync();
        var fieldId = form.Sections[0].Fields[0].Id;

        // Submit two results for the same form
        for (int i = 1; i <= 2; i++)
        {
            var resultDto = new CreateEvaluationResultDto
            {
                FormId = form.Id,
                EvaluatedBy = $"Evaluator {i}",
                Scores = new List<CreateEvaluationScoreDto>
                {
                    new() { FieldId = fieldId, Value = $"{i * 3}", NumericValue = i * 3.0 }
                }
            };
            await _client.PostAsJsonAsync("/api/evaluationresults", resultDto);
        }

        var response = await _client.GetAsync($"/api/evaluationresults/byform/{form.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<List<EvaluationResultDto>>();
        Assert.NotNull(results);
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(form.Id, r.FormId));
    }

    [Fact]
    public async Task CanRetrieveResultById()
    {
        var form = await CreateFormAsync();
        var fieldId = form.Sections[0].Fields[0].Id;

        var createDto = new CreateEvaluationResultDto
        {
            FormId = form.Id,
            EvaluatedBy = "Jane Smith",
            Scores = new List<CreateEvaluationScoreDto>
            {
                new() { FieldId = fieldId, Value = "7", NumericValue = 7.0 }
            }
        };

        var createResponse = await _client.PostAsJsonAsync("/api/evaluationresults", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<EvaluationResultDto>();
        Assert.NotNull(created);

        var response = await _client.GetAsync($"/api/evaluationresults/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<EvaluationResultDto>();
        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal("Jane Smith", result.EvaluatedBy);
    }

    [Fact]
    public async Task SubmitResultForNonExistentFormReturns404()
    {
        var resultDto = new CreateEvaluationResultDto
        {
            FormId = 99999,
            EvaluatedBy = "Test User",
            Scores = new List<CreateEvaluationScoreDto>()
        };

        var response = await _client.PostAsJsonAsync("/api/evaluationresults", resultDto);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Auto-TNI trigger tests ────────────────────────────────────────────────

    /// <summary>
    /// Creates a form with a rating field (MaxRating=5).
    /// A score of 3/5 (below max) should trigger auto-TNI creation.
    /// </summary>
    private async Task<(EvaluationFormDto Form, int FieldId)> CreateFormWithRatingFieldAsync(int maxRating = 5)
    {
        var formDto = new CreateEvaluationFormDto
        {
            Name = "TNI Trigger Test Form",
            Description = "Form for auto-TNI testing",
            Sections = new List<CreateFormSectionDto>
            {
                new()
                {
                    Title = "Compliance",
                    Order = 1,
                    Fields = new List<CreateFormFieldDto>
                    {
                        new() { Label = "CID Verification", FieldType = FieldType.Rating, IsRequired = true, Order = 1, MaxRating = maxRating }
                    }
                }
            }
        };
        var r = await _client.PostAsJsonAsync("/api/evaluationforms", formDto);
        r.EnsureSuccessStatusCode();
        var form = (await r.Content.ReadFromJsonAsync<EvaluationFormDto>())!;
        return (form, form.Sections[0].Fields[0].Id);
    }

    [Fact]
    public async Task SubmitResult_WithBelowMaxScore_AutoCreatesTniPlan()
    {
        var (form, fieldId) = await CreateFormWithRatingFieldAsync(maxRating: 5);

        var dto = new CreateEvaluationResultDto
        {
            FormId = form.Id,
            EvaluatedBy = "qa_user",
            AgentName = "Alice Agent",   // Required for auto-TNI to fire
            Scores = new List<CreateEvaluationScoreDto>
            {
                new() { FieldId = fieldId, Value = "3", NumericValue = 3.0 } // 3/5 = 60% — below max
            }
        };

        // Evaluation result creation must succeed even when TNI creation succeeds/fails
        var r = await _client.PostAsJsonAsync("/api/evaluationresults", dto);
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var result = await r.Content.ReadFromJsonAsync<EvaluationResultDto>();
        Assert.NotNull(result);

        // Brief delay to allow the fire-and-forget background LLM task to complete
        // (it will fail quickly — LLM not configured — and release the SQLite connection)
        await Task.Delay(300);

        // Verify TNI plan was created automatically
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plan = await db.TrainingPlans
            .Include(p => p.Items)
            .Where(p => p.EvaluationResultId == result!.Id && p.IsAutoGenerated)
            .FirstOrDefaultAsync();

        Assert.NotNull(plan);
        Assert.Equal("Alice Agent", plan!.AgentName);
        Assert.Equal("Draft", plan.Status);
        Assert.True(plan.IsAutoGenerated);
        Assert.NotEmpty(plan.Items);
        // The below-max field should be an item in the plan
        Assert.Contains(plan.Items, i => i.TargetArea == "CID Verification");
    }

    [Fact]
    public async Task SubmitResult_WithBinaryFail_AutoCreatesTniPlan()
    {
        var (form, fieldId) = await CreateFormWithRatingFieldAsync(maxRating: 1); // binary field

        var dto = new CreateEvaluationResultDto
        {
            FormId = form.Id,
            EvaluatedBy = "qa_user",
            AgentName = "Bob Agent",
            Scores = new List<CreateEvaluationScoreDto>
            {
                new() { FieldId = fieldId, Value = "0", NumericValue = 0.0 } // FAIL
            }
        };

        var r = await _client.PostAsJsonAsync("/api/evaluationresults", dto);
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var result = await r.Content.ReadFromJsonAsync<EvaluationResultDto>();

        await Task.Delay(300);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plan = await db.TrainingPlans
            .Where(p => p.EvaluationResultId == result!.Id && p.IsAutoGenerated)
            .FirstOrDefaultAsync();

        Assert.NotNull(plan);
        Assert.Equal("Bob Agent", plan!.AgentName);
    }

    [Fact]
    public async Task SubmitResult_WithPerfectScore_DoesNotCreateTniPlan()
    {
        var (form, fieldId) = await CreateFormWithRatingFieldAsync(maxRating: 5);

        var dto = new CreateEvaluationResultDto
        {
            FormId = form.Id,
            EvaluatedBy = "qa_user",
            AgentName = "Perfect Agent",
            Scores = new List<CreateEvaluationScoreDto>
            {
                new() { FieldId = fieldId, Value = "5", NumericValue = 5.0 } // max score — no TNI
            }
        };

        var r = await _client.PostAsJsonAsync("/api/evaluationresults", dto);
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var result = await r.Content.ReadFromJsonAsync<EvaluationResultDto>();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plan = await db.TrainingPlans
            .Where(p => p.EvaluationResultId == result!.Id && p.IsAutoGenerated)
            .FirstOrDefaultAsync();

        Assert.Null(plan); // No TNI for a perfect score
    }

    [Fact]
    public async Task SubmitResult_WithoutAgentName_DoesNotCreateTniPlan()
    {
        var (form, fieldId) = await CreateFormWithRatingFieldAsync(maxRating: 5);

        var dto = new CreateEvaluationResultDto
        {
            FormId = form.Id,
            EvaluatedBy = "qa_user",
            AgentName = null,  // no agent name → no auto-TNI
            Scores = new List<CreateEvaluationScoreDto>
            {
                new() { FieldId = fieldId, Value = "2", NumericValue = 2.0 } // below max
            }
        };

        var r = await _client.PostAsJsonAsync("/api/evaluationresults", dto);
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var result = await r.Content.ReadFromJsonAsync<EvaluationResultDto>();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // No plan since agentName was not provided
        var planCount = await db.TrainingPlans
            .CountAsync(p => p.EvaluationResultId == result!.Id && p.IsAutoGenerated);
        Assert.Equal(0, planCount);
    }

    [Fact]
    public async Task SubmitResult_WithBelowMaxScore_TniPlanHasEvaluationResultLink()
    {
        var (form, fieldId) = await CreateFormWithRatingFieldAsync(maxRating: 5);

        var dto = new CreateEvaluationResultDto
        {
            FormId = form.Id,
            EvaluatedBy = "qa_user",
            AgentName = "Carol Agent",
            AgentUsername = "carol",
            Scores = new List<CreateEvaluationScoreDto>
            {
                new() { FieldId = fieldId, Value = "1", NumericValue = 1.0 } // 1/5
            }
        };

        var r = await _client.PostAsJsonAsync("/api/evaluationresults", dto);
        r.EnsureSuccessStatusCode();
        var result = await r.Content.ReadFromJsonAsync<EvaluationResultDto>();

        await Task.Delay(300);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plan = await db.TrainingPlans
            .Where(p => p.EvaluationResultId == result!.Id && p.IsAutoGenerated)
            .FirstOrDefaultAsync();

        Assert.NotNull(plan);
        Assert.Equal(result!.Id, plan!.EvaluationResultId);
        Assert.Equal("carol", plan.AgentUsername);
        Assert.Equal("qa_user", plan.TrainerName);
    }
}
