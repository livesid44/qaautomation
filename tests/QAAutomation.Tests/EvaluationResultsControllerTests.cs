using System.Net;
using System.Net.Http.Json;
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
}
