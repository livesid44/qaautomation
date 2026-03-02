using System.Net;
using System.Net.Http.Json;
using QAAutomation.API.DTOs;
using QAAutomation.API.Models;
using Xunit;

namespace QAAutomation.Tests;

public class AnalyticsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AnalyticsControllerTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAnalytics_Returns200WithValidStructure()
    {
        var response = await _client.GetAsync("/api/analytics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<AnalyticsDto>();
        Assert.NotNull(dto);
        // All list properties must be non-null (may be empty or populated from seed data)
        Assert.NotNull(dto!.DailyScores);
        Assert.NotNull(dto.AgentScores);
        Assert.NotNull(dto.ParameterTrends);
        Assert.NotNull(dto.CallTypeScores);
        Assert.True(dto.TotalAudits >= 0);
    }

    [Fact]
    public async Task GetAnalytics_AfterAuditSubmitted_ReturnsAggregatedData()
    {
        // Create a form with one rating field
        var formDto = new CreateEvaluationFormDto
        {
            Name = "Analytics Test Form",
            Sections = new List<CreateFormSectionDto>
            {
                new()
                {
                    Title = "Communication",
                    Order = 1,
                    Fields = new List<CreateFormFieldDto>
                    {
                        new() { Label = "Greeting", FieldType = FieldType.Rating, IsRequired = true, Order = 1, MaxRating = 5 }
                    }
                }
            }
        };
        var formResp = await _client.PostAsJsonAsync("/api/evaluationforms", formDto);
        formResp.EnsureSuccessStatusCode();
        var form = await formResp.Content.ReadFromJsonAsync<EvaluationFormDto>();
        var fieldId = form!.Sections[0].Fields[0].Id;

        // Submit two audit records for different agents
        foreach (var (agent, score) in new[] { ("Alice", 4.0), ("Bob", 2.0) })
        {
            var auditDto = new CreateEvaluationResultDto
            {
                FormId = form.Id,
                EvaluatedBy = "QA Team",
                AgentName = agent,
                CallDate = new DateTime(2025, 6, 15),
                Scores = new List<CreateEvaluationScoreDto>
                {
                    new() { FieldId = fieldId, Value = score.ToString(), NumericValue = score }
                }
            };
            var r = await _client.PostAsJsonAsync("/api/evaluationresults", auditDto);
            r.EnsureSuccessStatusCode();
        }

        var response = await _client.GetAsync("/api/analytics");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<AnalyticsDto>();
        Assert.NotNull(dto);
        Assert.True(dto!.TotalAudits >= 2);

        // Daily: both audits on the same day → one daily entry
        Assert.Contains(dto.DailyScores, d => d.Date == "2025-06-15");

        // Agents: Alice and Bob present
        Assert.Contains(dto.AgentScores, a => a.AgentName == "Alice");
        Assert.Contains(dto.AgentScores, a => a.AgentName == "Bob");

        // Alice score = 4/5 = 80%, Bob = 2/5 = 40%
        var alice = dto.AgentScores.First(a => a.AgentName == "Alice");
        Assert.Equal(80.0, alice.AvgScorePercent);

        // Parameter trend contains the "Greeting" field
        Assert.Contains(dto.ParameterTrends, p => p.ParameterLabel == "Greeting");

        // Call-type trend contains the form
        Assert.Contains(dto.CallTypeScores, c => c.FormName == "Analytics Test Form");
    }
}
