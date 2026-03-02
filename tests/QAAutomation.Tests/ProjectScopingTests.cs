using System.Net;
using System.Net.Http.Json;
using QAAutomation.API.DTOs;
using QAAutomation.API.Models;
using Xunit;

namespace QAAutomation.Tests;

/// <summary>
/// Integration tests verifying that configuration data and audit records are
/// correctly scoped per-project and are not visible across projects.
/// </summary>
public class ProjectScopingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProjectScopingTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<int> CreateProjectAsync(string name)
    {
        var resp = await _client.PostAsJsonAsync("/api/projects", new { name, description = name, isActive = true });
        resp.EnsureSuccessStatusCode();
        var project = await resp.Content.ReadFromJsonAsync<ProjectDto>();
        return project!.Id;
    }

    private async Task<EvaluationFormDto> CreateFormWithLobAsync(int projectId, string formName)
    {
        // Create a LOB under the project
        var lobResp = await _client.PostAsJsonAsync("/api/lobs", new { projectId, name = formName + " LOB", isActive = true });
        lobResp.EnsureSuccessStatusCode();
        var lob = await lobResp.Content.ReadFromJsonAsync<LobDto>();

        var formDto = new CreateEvaluationFormDto
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
                        new() { Label = "Score", FieldType = FieldType.Rating, MaxRating = 5, IsRequired = true, Order = 1 }
                    }
                }
            }
        };
        var formResp = await _client.PostAsJsonAsync("/api/evaluationforms", formDto);
        formResp.EnsureSuccessStatusCode();
        return (await formResp.Content.ReadFromJsonAsync<EvaluationFormDto>())!;
    }

    // ── EvaluationResults (Audits) scoping ────────────────────────────────────

    [Fact]
    public async Task Audits_FilteredByProject_DoNotLeakAcrossProjects()
    {
        int projectA = await CreateProjectAsync("Audit-ProjectA");
        int projectB = await CreateProjectAsync("Audit-ProjectB");

        var formA = await CreateFormWithLobAsync(projectA, "Form-A");
        var formB = await CreateFormWithLobAsync(projectB, "Form-B");

        int fieldA = formA.Sections[0].Fields[0].Id;
        int fieldB = formB.Sections[0].Fields[0].Id;

        // Create one audit under each project
        await _client.PostAsJsonAsync("/api/evaluationresults", new CreateEvaluationResultDto
        {
            FormId = formA.Id, EvaluatedBy = "tester", AgentName = "AgentA",
            Scores = new() { new() { FieldId = fieldA, Value = "4", NumericValue = 4 } }
        });
        await _client.PostAsJsonAsync("/api/evaluationresults", new CreateEvaluationResultDto
        {
            FormId = formB.Id, EvaluatedBy = "tester", AgentName = "AgentB",
            Scores = new() { new() { FieldId = fieldB, Value = "2", NumericValue = 2 } }
        });

        // Project A filter should return only AgentA
        var respA = await _client.GetFromJsonAsync<List<EvaluationResultDto>>($"/api/evaluationresults?projectId={projectA}");
        Assert.NotNull(respA);
        Assert.All(respA!, r => Assert.Equal("AgentA", r.AgentName));
        Assert.DoesNotContain(respA!, r => r.AgentName == "AgentB");

        // Project B filter should return only AgentB
        var respB = await _client.GetFromJsonAsync<List<EvaluationResultDto>>($"/api/evaluationresults?projectId={projectB}");
        Assert.NotNull(respB);
        Assert.All(respB!, r => Assert.Equal("AgentB", r.AgentName));
        Assert.DoesNotContain(respB!, r => r.AgentName == "AgentA");
    }

    // ── Analytics scoping ─────────────────────────────────────────────────────

    [Fact]
    public async Task Analytics_FilteredByProject_DoNotLeakAcrossProjects()
    {
        int projectA = await CreateProjectAsync("Analytics-ProjectA");
        int projectB = await CreateProjectAsync("Analytics-ProjectB");

        var formA = await CreateFormWithLobAsync(projectA, "Analytics-Form-A");
        var formB = await CreateFormWithLobAsync(projectB, "Analytics-Form-B");

        int fieldA = formA.Sections[0].Fields[0].Id;
        int fieldB = formB.Sections[0].Fields[0].Id;

        await _client.PostAsJsonAsync("/api/evaluationresults", new CreateEvaluationResultDto
        {
            FormId = formA.Id, EvaluatedBy = "tester", AgentName = "AlphaAgent",
            CallDate = new DateTime(2025, 3, 1),
            Scores = new() { new() { FieldId = fieldA, Value = "5", NumericValue = 5 } }
        });
        await _client.PostAsJsonAsync("/api/evaluationresults", new CreateEvaluationResultDto
        {
            FormId = formB.Id, EvaluatedBy = "tester", AgentName = "BetaAgent",
            CallDate = new DateTime(2025, 3, 1),
            Scores = new() { new() { FieldId = fieldB, Value = "1", NumericValue = 1 } }
        });

        var analyticsA = await _client.GetFromJsonAsync<AnalyticsDto>($"/api/analytics?projectId={projectA}");
        Assert.NotNull(analyticsA);
        Assert.Contains(analyticsA!.AgentScores, a => a.AgentName == "AlphaAgent");
        Assert.DoesNotContain(analyticsA.AgentScores, a => a.AgentName == "BetaAgent");

        var analyticsB = await _client.GetFromJsonAsync<AnalyticsDto>($"/api/analytics?projectId={projectB}");
        Assert.NotNull(analyticsB);
        Assert.Contains(analyticsB!.AgentScores, a => a.AgentName == "BetaAgent");
        Assert.DoesNotContain(analyticsB.AgentScores, a => a.AgentName == "AlphaAgent");
    }

    // ── Parameters scoping ────────────────────────────────────────────────────

    [Fact]
    public async Task Parameters_FilteredByProject_DoNotLeakAcrossProjects()
    {
        int projectA = await CreateProjectAsync("Param-ProjectA");
        int projectB = await CreateProjectAsync("Param-ProjectB");

        await _client.PostAsJsonAsync("/api/parameters", new
        {
            name = "ParamOnlyInA", projectId = projectA, category = "X", isActive = true
        });
        await _client.PostAsJsonAsync("/api/parameters", new
        {
            name = "ParamOnlyInB", projectId = projectB, category = "X", isActive = true
        });

        var paramsA = await _client.GetFromJsonAsync<List<ParameterDto>>($"/api/parameters?projectId={projectA}");
        Assert.NotNull(paramsA);
        Assert.Contains(paramsA!, p => p.Name == "ParamOnlyInA");
        Assert.DoesNotContain(paramsA!, p => p.Name == "ParamOnlyInB");

        var paramsB = await _client.GetFromJsonAsync<List<ParameterDto>>($"/api/parameters?projectId={projectB}");
        Assert.NotNull(paramsB);
        Assert.Contains(paramsB!, p => p.Name == "ParamOnlyInB");
        Assert.DoesNotContain(paramsB!, p => p.Name == "ParamOnlyInA");
    }

    // ── RatingCriteria scoping ─────────────────────────────────────────────────

    [Fact]
    public async Task RatingCriteria_FilteredByProject_DoNotLeakAcrossProjects()
    {
        int projectA = await CreateProjectAsync("RC-ProjectA");
        int projectB = await CreateProjectAsync("RC-ProjectB");

        await _client.PostAsJsonAsync("/api/ratingcriteria", new
        {
            name = "CriteriaOnlyInA", projectId = projectA, minScore = 1, maxScore = 5,
            levels = new object[] { new { score = 1, label = "Low", color = "#ff0000" } }
        });
        await _client.PostAsJsonAsync("/api/ratingcriteria", new
        {
            name = "CriteriaOnlyInB", projectId = projectB, minScore = 1, maxScore = 5,
            levels = new object[] { new { score = 1, label = "Low", color = "#ff0000" } }
        });

        var criteriaA = await _client.GetFromJsonAsync<List<RatingCriteriaDto>>($"/api/ratingcriteria?projectId={projectA}");
        Assert.NotNull(criteriaA);
        Assert.Contains(criteriaA!, c => c.Name == "CriteriaOnlyInA");
        Assert.DoesNotContain(criteriaA!, c => c.Name == "CriteriaOnlyInB");

        var criteriaB = await _client.GetFromJsonAsync<List<RatingCriteriaDto>>($"/api/ratingcriteria?projectId={projectB}");
        Assert.NotNull(criteriaB);
        Assert.Contains(criteriaB!, c => c.Name == "CriteriaOnlyInB");
        Assert.DoesNotContain(criteriaB!, c => c.Name == "CriteriaOnlyInA");
    }

    // ── KnowledgeBase scoping ─────────────────────────────────────────────────

    [Fact]
    public async Task KnowledgeSources_FilteredByProject_DoNotLeakAcrossProjects()
    {
        int projectA = await CreateProjectAsync("KB-ProjectA");
        int projectB = await CreateProjectAsync("KB-ProjectB");

        await _client.PostAsJsonAsync("/api/knowledgebase/sources", new KnowledgeSourceDto
        {
            Name = "SourceOnlyInA", ConnectorType = "ManualUpload", IsActive = true, ProjectId = projectA
        });
        await _client.PostAsJsonAsync("/api/knowledgebase/sources", new KnowledgeSourceDto
        {
            Name = "SourceOnlyInB", ConnectorType = "ManualUpload", IsActive = true, ProjectId = projectB
        });

        var sourcesA = await _client.GetFromJsonAsync<List<KnowledgeSourceDto>>($"/api/knowledgebase/sources?projectId={projectA}");
        Assert.NotNull(sourcesA);
        Assert.Contains(sourcesA!, s => s.Name == "SourceOnlyInA");
        Assert.DoesNotContain(sourcesA!, s => s.Name == "SourceOnlyInB");

        var sourcesB = await _client.GetFromJsonAsync<List<KnowledgeSourceDto>>($"/api/knowledgebase/sources?projectId={projectB}");
        Assert.NotNull(sourcesB);
        Assert.Contains(sourcesB!, s => s.Name == "SourceOnlyInB");
        Assert.DoesNotContain(sourcesB!, s => s.Name == "SourceOnlyInA");
    }
}
