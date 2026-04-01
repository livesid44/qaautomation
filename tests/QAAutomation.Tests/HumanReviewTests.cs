using System.Net.Http.Json;
using QAAutomation.API.DTOs;
using QAAutomation.API.Models;
using Xunit;

namespace QAAutomation.Tests;

/// <summary>
/// Integration tests for the Human-in-the-Loop (HITL) module:
///   • SamplingPoliciesController — CRUD + apply (percentage and count)
///   • HumanReviewController — list queue, get item, submit review
/// </summary>
public class HumanReviewTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HumanReviewTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private async Task<int> CreateProjectAsync(string name)
    {
        var r = await _client.PostAsJsonAsync("/api/projects",
            new { name, description = name, isActive = true });
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<ProjectDto>())!.Id;
    }

    private async Task<int> CreateFormAsync(int projectId, string formName)
    {
        var lobR = await _client.PostAsJsonAsync("/api/lobs",
            new { projectId, name = formName + " LOB", isActive = true });
        lobR.EnsureSuccessStatusCode();
        var lob = await lobR.Content.ReadFromJsonAsync<LobDto>();

        var formR = await _client.PostAsJsonAsync("/api/evaluationforms", new CreateEvaluationFormDto
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
                        new() { Label = "Quality", FieldType = FieldType.Rating, MaxRating = 5, IsRequired = true, Order = 1 }
                    }
                }
            }
        });
        formR.EnsureSuccessStatusCode();
        return (await formR.Content.ReadFromJsonAsync<EvaluationFormDto>())!.Id;
    }

    private async Task<int> CreateEvaluationResultAsync(int formId, string agentName, string callRef)
    {
        var r = await _client.PostAsJsonAsync("/api/evaluationresults", new
        {
            formId,
            evaluatedBy = "system",
            agentName,
            callReference = callRef,
            scores = new[] { new { fieldId = 0, value = "3", numericValue = 3.0 } }
        });
        // We can't know fieldId easily, so query the form and use first field
        // Instead create via direct model. For test we just read back and ignore field validations.
        // Return the seeded result id from the database state.
        if (r.IsSuccessStatusCode)
        {
            var result = await r.Content.ReadFromJsonAsync<EvaluationResultDto>();
            return result!.Id;
        }
        return 0;
    }

    // ── Sampling Policy CRUD ──────────────────────────────────────────────────

    [Fact]
    public async Task CreatePolicy_ValidPercentage_Returns201()
    {
        var dto = new
        {
            name = "10% Random Sample",
            description = "Sample 10% of all calls",
            samplingMethod = "Percentage",
            sampleValue = 10.0f,
            isActive = true,
            createdBy = "admin"
        };
        var r = await _client.PostAsJsonAsync("/api/samplingpolicies", dto);
        Assert.Equal(System.Net.HttpStatusCode.Created, r.StatusCode);
        var created = await r.Content.ReadFromJsonAsync<SamplingPolicyDto>();
        Assert.NotNull(created);
        Assert.Equal("10% Random Sample", created!.Name);
        Assert.Equal("Percentage", created.SamplingMethod);
        Assert.Equal(10f, created.SampleValue);
        Assert.True(created.IsActive);
    }

    [Fact]
    public async Task CreatePolicy_ValidCount_Returns201()
    {
        var dto = new
        {
            name = "Fixed 5 Calls",
            samplingMethod = "Count",
            sampleValue = 5.0f,
            isActive = true,
            createdBy = "admin"
        };
        var r = await _client.PostAsJsonAsync("/api/samplingpolicies", dto);
        Assert.Equal(System.Net.HttpStatusCode.Created, r.StatusCode);
        var created = await r.Content.ReadFromJsonAsync<SamplingPolicyDto>();
        Assert.Equal("Count", created!.SamplingMethod);
        Assert.Equal(5f, created.SampleValue);
    }

    [Fact]
    public async Task CreatePolicy_InvalidMethod_Returns400()
    {
        var dto = new
        {
            name = "Bad Method",
            samplingMethod = "RANDOM",
            sampleValue = 10f,
            isActive = true,
            createdBy = "admin"
        };
        var r = await _client.PostAsJsonAsync("/api/samplingpolicies", dto);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task CreatePolicy_PercentageOver100_Returns400()
    {
        var dto = new
        {
            name = "Over 100%",
            samplingMethod = "Percentage",
            sampleValue = 150f,
            isActive = true,
            createdBy = "admin"
        };
        var r = await _client.PostAsJsonAsync("/api/samplingpolicies", dto);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task GetAllPolicies_ReturnsList()
    {
        // Create a policy first
        await _client.PostAsJsonAsync("/api/samplingpolicies", new
        {
            name = "List Test Policy",
            samplingMethod = "Percentage",
            sampleValue = 20f,
            isActive = true,
            createdBy = "admin"
        });

        var r = await _client.GetFromJsonAsync<List<SamplingPolicyDto>>("/api/samplingpolicies");
        Assert.NotNull(r);
        Assert.True(r!.Count >= 1);
    }

    [Fact]
    public async Task UpdatePolicy_ChangesName()
    {
        // Create
        var createR = await _client.PostAsJsonAsync("/api/samplingpolicies", new
        {
            name = "Update Test",
            samplingMethod = "Count",
            sampleValue = 3f,
            isActive = true,
            createdBy = "admin"
        });
        var created = await createR.Content.ReadFromJsonAsync<SamplingPolicyDto>();

        // Update
        var updateR = await _client.PutAsJsonAsync($"/api/samplingpolicies/{created!.Id}", new
        {
            name = "Updated Name",
            samplingMethod = "Count",
            sampleValue = 5f,
            isActive = false
        });
        Assert.True(updateR.IsSuccessStatusCode);
        var updated = await updateR.Content.ReadFromJsonAsync<SamplingPolicyDto>();
        Assert.Equal("Updated Name", updated!.Name);
        Assert.Equal(5f, updated.SampleValue);
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task DeletePolicy_Returns204()
    {
        var createR = await _client.PostAsJsonAsync("/api/samplingpolicies", new
        {
            name = "To Delete",
            samplingMethod = "Percentage",
            sampleValue = 5f,
            isActive = true,
            createdBy = "admin"
        });
        var created = await createR.Content.ReadFromJsonAsync<SamplingPolicyDto>();

        var deleteR = await _client.DeleteAsync($"/api/samplingpolicies/{created!.Id}");
        Assert.Equal(System.Net.HttpStatusCode.NoContent, deleteR.StatusCode);

        var getR = await _client.GetAsync($"/api/samplingpolicies/{created.Id}");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, getR.StatusCode);
    }

    // ── Sampling Apply ────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyPolicy_Percentage_SamplesCorrectCount()
    {
        // Create a policy that samples 100% → all eligible items should be enqueued
        var policyR = await _client.PostAsJsonAsync("/api/samplingpolicies", new
        {
            name = "100% Apply Test",
            samplingMethod = "Percentage",
            sampleValue = 100f,
            isActive = true,
            createdBy = "admin"
        });
        var policy = await policyR.Content.ReadFromJsonAsync<SamplingPolicyDto>();

        // The DB already has seeded evaluation results from the test factory.
        // Apply policy and verify response structure.
        var applyR = await _client.PostAsync($"/api/samplingpolicies/{policy!.Id}/apply?appliedBy=admin", null);
        Assert.True(applyR.IsSuccessStatusCode);
        var result = await applyR.Content.ReadFromJsonAsync<SamplingApplyResultDto>();
        Assert.NotNull(result);
        Assert.Equal(policy.Id, result!.PolicyId);
        // Sampled count should be eligible count (100%)
        Assert.Equal(result.EligibleCount, result.SampledCount);
    }

    [Fact]
    public async Task ApplyPolicy_Count_SamplesUpToCount()
    {
        var policyR = await _client.PostAsJsonAsync("/api/samplingpolicies", new
        {
            name = "Count 2 Test",
            samplingMethod = "Count",
            sampleValue = 2f,
            isActive = true,
            createdBy = "admin"
        });
        var policy = await policyR.Content.ReadFromJsonAsync<SamplingPolicyDto>();

        var applyR = await _client.PostAsync($"/api/samplingpolicies/{policy!.Id}/apply?appliedBy=admin", null);
        Assert.True(applyR.IsSuccessStatusCode);
        var result = await applyR.Content.ReadFromJsonAsync<SamplingApplyResultDto>();
        // Sampled count should be at most 2
        Assert.True(result!.SampledCount <= 2);
    }

    [Fact]
    public async Task ApplyPolicy_Twice_DoesNotDuplicateItems()
    {
        var policyR = await _client.PostAsJsonAsync("/api/samplingpolicies", new
        {
            name = "NoDupe Test",
            samplingMethod = "Percentage",
            sampleValue = 100f,
            isActive = true,
            createdBy = "admin"
        });
        var policy = await policyR.Content.ReadFromJsonAsync<SamplingPolicyDto>();

        // Apply once — get the queue count before and after
        var queueBefore = (await _client.GetFromJsonAsync<List<HumanReviewItemDto>>("/api/humanreview"))!.Count;
        var r1 = await _client.PostAsync($"/api/samplingpolicies/{policy!.Id}/apply?appliedBy=admin", null);
        var res1 = await r1.Content.ReadFromJsonAsync<SamplingApplyResultDto>();

        // Apply again — no new items should be created (dedup on EvaluationResultId)
        var r2 = await _client.PostAsync($"/api/samplingpolicies/{policy.Id}/apply?appliedBy=admin", null);
        var res2 = await r2.Content.ReadFromJsonAsync<SamplingApplyResultDto>();
        Assert.Equal(0, res2!.SampledCount);

        // Verify total queue grew by exactly res1.SampledCount
        var queueAfter = (await _client.GetFromJsonAsync<List<HumanReviewItemDto>>("/api/humanreview"))!.Count;
        Assert.Equal(queueBefore + res1!.SampledCount, queueAfter);
    }

    // ── Human Review Queue ────────────────────────────────────────────────────

    [Fact]
    public async Task GetReviewQueue_ReturnsItems()
    {
        // Apply a policy to ensure there are items
        var policyR = await _client.PostAsJsonAsync("/api/samplingpolicies", new
        {
            name = "Queue List Test",
            samplingMethod = "Percentage",
            sampleValue = 100f,
            isActive = true,
            createdBy = "admin"
        });
        var policy = await policyR.Content.ReadFromJsonAsync<SamplingPolicyDto>();
        await _client.PostAsync($"/api/samplingpolicies/{policy!.Id}/apply?appliedBy=admin", null);

        var queue = await _client.GetFromJsonAsync<List<HumanReviewItemDto>>("/api/humanreview");
        Assert.NotNull(queue);
        Assert.True(queue!.Count > 0);
    }

    [Fact]
    public async Task SubmitReview_SetsStatusReviewed()
    {
        // Ensure at least one item in queue
        var policyR = await _client.PostAsJsonAsync("/api/samplingpolicies", new
        {
            name = "Submit Review Test Policy",
            samplingMethod = "Percentage",
            sampleValue = 100f,
            isActive = true,
            createdBy = "admin"
        });
        var policy = await policyR.Content.ReadFromJsonAsync<SamplingPolicyDto>();
        await _client.PostAsync($"/api/samplingpolicies/{policy!.Id}/apply?appliedBy=admin", null);

        var queue = await _client.GetFromJsonAsync<List<HumanReviewItemDto>>("/api/humanreview?status=Pending");
        Assert.NotNull(queue);
        Assert.True(queue!.Count > 0, "Expected at least one Pending review item");

        var item = queue.First();

        // Submit a review
        var reviewR = await _client.PutAsJsonAsync($"/api/humanreview/{item.Id}/review", new
        {
            reviewerComment = "Looks correct to me.",
            reviewVerdict = "Agree",
            reviewedBy = "qa_user"
        });
        Assert.True(reviewR.IsSuccessStatusCode);

        var reviewed = await reviewR.Content.ReadFromJsonAsync<HumanReviewItemDto>();
        Assert.Equal("Reviewed", reviewed!.Status);
        Assert.Equal("Agree", reviewed.ReviewVerdict);
        Assert.Equal("qa_user", reviewed.ReviewedBy);
        Assert.Equal("Looks correct to me.", reviewed.ReviewerComment);
        Assert.NotNull(reviewed.ReviewedAt);
    }

    [Fact]
    public async Task SubmitReview_InvalidVerdict_Returns400()
    {
        var queue = await _client.GetFromJsonAsync<List<HumanReviewItemDto>>("/api/humanreview");
        if (queue == null || queue.Count == 0) return; // no items, skip

        var item = queue.First();
        var r = await _client.PutAsJsonAsync($"/api/humanreview/{item.Id}/review", new
        {
            reviewerComment = "test",
            reviewVerdict = "Maybe",  // invalid
            reviewedBy = "qa_user"
        });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task FilterQueue_ByStatus_ReturnsOnlyMatchingItems()
    {
        var queue = await _client.GetFromJsonAsync<List<HumanReviewItemDto>>("/api/humanreview?status=Reviewed");
        Assert.NotNull(queue);
        Assert.All(queue!, item => Assert.Equal("Reviewed", item.Status));
    }

    [Fact]
    public async Task StartReview_ChangesPendingToInReview()
    {
        // Ensure items exist
        var policyR = await _client.PostAsJsonAsync("/api/samplingpolicies", new
        {
            name = "Start Review Test Policy",
            samplingMethod = "Count",
            sampleValue = 1f,
            isActive = true,
            createdBy = "admin"
        });
        var policy = await policyR.Content.ReadFromJsonAsync<SamplingPolicyDto>();
        await _client.PostAsync($"/api/samplingpolicies/{policy!.Id}/apply?appliedBy=admin", null);

        var pending = await _client.GetFromJsonAsync<List<HumanReviewItemDto>>("/api/humanreview?status=Pending");
        if (pending == null || pending.Count == 0) return;

        var item = pending.First();
        var startR = await _client.PutAsync($"/api/humanreview/{item.Id}/start?reviewer=qa_analyst", null);
        Assert.True(startR.IsSuccessStatusCode);

        var updated = await startR.Content.ReadFromJsonAsync<HumanReviewItemDto>();
        Assert.Equal("InReview", updated!.Status);
        Assert.Equal("qa_analyst", updated.AssignedTo);
    }

    [Fact]
    public async Task AddManualReview_NonExistentResult_Returns400()
    {
        var r = await _client.PostAsJsonAsync("/api/humanreview/manual", new
        {
            evaluationResultId = 999999,
            addedBy = "admin"
        });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, r.StatusCode);
    }

    // ── Per-parameter human score tests ──────────────────────────────────────

    [Fact]
    public async Task SubmitReview_WithFieldScores_StoresPerParameterHumanScores()
    {
        // Use a seeded evaluation result (guaranteed to have valid field IDs)
        var allResults = await _client.GetFromJsonAsync<List<EvaluationResultDto>>("/api/evaluationresults");
        Assert.NotNull(allResults);
        Assert.NotEmpty(allResults!);

        var evalResult = allResults.First(r => r.Sections.Any(s => s.Fields.Any()));
        var firstField = evalResult.Sections.First().Fields.First();

        // Create a manual review item pointing to this evaluation result
        var manualR = await _client.PostAsJsonAsync("/api/humanreview/manual",
            new { evaluationResultId = evalResult.Id, addedBy = "admin" });
        // May already be queued — accept 201 Created or 409 Conflict
        Assert.True(manualR.StatusCode is System.Net.HttpStatusCode.Created or System.Net.HttpStatusCode.Conflict,
            $"Unexpected status: {manualR.StatusCode}");

        // If conflict (already queued), get the existing item; otherwise use the one just created
        int reviewItemId;
        if (manualR.IsSuccessStatusCode)
        {
            var created = await manualR.Content.ReadFromJsonAsync<HumanReviewItemDto>();
            reviewItemId = created!.Id;
        }
        else
        {
            var queue = await _client.GetFromJsonAsync<List<HumanReviewItemDto>>("/api/humanreview");
            var existing = queue!.First(q => q.EvaluationResultId == evalResult.Id);
            reviewItemId = existing.Id;
        }

        // Submit verdict with per-field human scores
        var reviewR = await _client.PutAsJsonAsync($"/api/humanreview/{reviewItemId}/review", new
        {
            reviewerComment = "AI was slightly generous.",
            reviewVerdict   = "Partial",
            reviewedBy      = "qa_analyst",
            fieldScores     = new[]
            {
                new { fieldId = firstField.FieldId, aiScore = firstField.NumericValue ?? 3.0, humanScore = 2.0, comment = "Score adjusted" }
            }
        });

        Assert.True(reviewR.IsSuccessStatusCode, $"Expected success, got {reviewR.StatusCode}");

        var result = await reviewR.Content.ReadFromJsonAsync<HumanReviewItemDto>();
        Assert.NotNull(result);
        Assert.Equal("Reviewed", result!.Status);
        Assert.Equal("Partial", result.ReviewVerdict);
        Assert.NotEmpty(result.FieldScores);
        var fs = result.FieldScores.First(s => s.FieldId == firstField.FieldId);
        Assert.Equal(2.0, fs.HumanScore);
        Assert.Equal("Score adjusted", fs.Comment);
    }

    [Fact]
    public async Task SubmitReview_WithFieldScores_GetById_ReturnsScores()
    {
        // Use a seeded evaluation result
        var allResults = await _client.GetFromJsonAsync<List<EvaluationResultDto>>("/api/evaluationresults");
        Assert.NotNull(allResults);
        var evalResult = allResults!.First(r => r.Sections.Any(s => s.Fields.Any()));
        var firstField = evalResult.Sections.First().Fields.First();

        // Enqueue or retrieve existing review item
        var manualR = await _client.PostAsJsonAsync("/api/humanreview/manual",
            new { evaluationResultId = evalResult.Id, addedBy = "admin" });
        int reviewItemId;
        if (manualR.IsSuccessStatusCode)
        {
            var created = await manualR.Content.ReadFromJsonAsync<HumanReviewItemDto>();
            reviewItemId = created!.Id;
        }
        else
        {
            var queue = await _client.GetFromJsonAsync<List<HumanReviewItemDto>>("/api/humanreview");
            reviewItemId = queue!.First(q => q.EvaluationResultId == evalResult.Id).Id;
        }

        // Submit with field scores
        await _client.PutAsJsonAsync($"/api/humanreview/{reviewItemId}/review", new
        {
            reviewVerdict = "Disagree",
            reviewedBy    = "qa_tester",
            fieldScores   = new[] { new { fieldId = firstField.FieldId, aiScore = 5.0, humanScore = 1.0, comment = "Wrong" } }
        });

        // GET by ID should return the field scores
        var item = await _client.GetFromJsonAsync<HumanReviewItemDto>($"/api/humanreview/{reviewItemId}");
        Assert.NotNull(item);
        Assert.NotEmpty(item!.FieldScores);
        var fs = item.FieldScores.FirstOrDefault(s => s.FieldId == firstField.FieldId);
        Assert.NotNull(fs);
        Assert.Equal(1.0, fs!.HumanScore);
    }

    [Fact]
    public async Task HitlComparison_WithReviewedScores_ReturnsComparison()
    {
        // Use a seeded evaluation result
        var allResults = await _client.GetFromJsonAsync<List<EvaluationResultDto>>("/api/evaluationresults");
        Assert.NotNull(allResults);
        var evalResult = allResults!.First(r => r.Sections.Any(s => s.Fields.Any()));
        var firstField = evalResult.Sections.First().Fields.First();

        // Enqueue or retrieve
        var manualR = await _client.PostAsJsonAsync("/api/humanreview/manual",
            new { evaluationResultId = evalResult.Id, addedBy = "admin" });
        int reviewItemId;
        if (manualR.IsSuccessStatusCode)
        {
            var created = await manualR.Content.ReadFromJsonAsync<HumanReviewItemDto>();
            reviewItemId = created!.Id;
        }
        else
        {
            var queue = await _client.GetFromJsonAsync<List<HumanReviewItemDto>>("/api/humanreview");
            reviewItemId = queue!.First(q => q.EvaluationResultId == evalResult.Id).Id;
        }

        // Submit review with field scores
        await _client.PutAsJsonAsync($"/api/humanreview/{reviewItemId}/review", new
        {
            reviewVerdict = "Disagree",
            reviewedBy    = "qa_tester",
            fieldScores   = new[] { new { fieldId = firstField.FieldId, aiScore = 4.0, humanScore = 2.0, comment = (string?)null } }
        });

        // Act: get comparison
        var comparison = await _client.GetFromJsonAsync<HitlComparisonDto>("/api/analytics/hitl-comparison");

        // Assert
        Assert.NotNull(comparison);
        Assert.True(comparison!.ReviewedWithScores >= 1);
        Assert.NotEmpty(comparison.ParameterComparison);
        Assert.NotEmpty(comparison.SectionComparison);
    }
}
