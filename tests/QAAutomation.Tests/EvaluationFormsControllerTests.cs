using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using QAAutomation.API.Data;
using QAAutomation.API.DTOs;
using QAAutomation.API.Models;
using Xunit;

namespace QAAutomation.Tests;

public class EvaluationFormsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public EvaluationFormsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private CreateEvaluationFormDto BuildCreateDto(string name = "Test Form") => new()
    {
        Name = name,
        Description = "A test form",
        Sections = new List<CreateFormSectionDto>
        {
            new()
            {
                Title = "Section 1",
                Order = 1,
                Fields = new List<CreateFormFieldDto>
                {
                    new() { Label = "Rating Field", FieldType = FieldType.Rating, IsRequired = true, Order = 1, MaxRating = 5 },
                    new() { Label = "Text Field", FieldType = FieldType.Text, IsRequired = false, Order = 2 }
                }
            }
        }
    };

    [Fact]
    public async Task CanCreateFormWithSectionsAndFields()
    {
        var dto = BuildCreateDto("Create Test Form");

        var response = await _client.PostAsJsonAsync("/api/evaluationforms", dto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<EvaluationFormDto>();
        Assert.NotNull(created);
        Assert.Equal("Create Test Form", created.Name);
        Assert.Single(created.Sections);
        Assert.Equal(2, created.Sections[0].Fields.Count);
    }

    [Fact]
    public async Task CanRetrieveFormById()
    {
        var dto = BuildCreateDto("Retrieve By Id Form");
        var createResponse = await _client.PostAsJsonAsync("/api/evaluationforms", dto);
        var created = await createResponse.Content.ReadFromJsonAsync<EvaluationFormDto>();
        Assert.NotNull(created);

        var response = await _client.GetAsync($"/api/evaluationforms/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var form = await response.Content.ReadFromJsonAsync<EvaluationFormDto>();
        Assert.NotNull(form);
        Assert.Equal(created.Id, form.Id);
        Assert.Equal("Retrieve By Id Form", form.Name);
        Assert.Single(form.Sections);
    }

    [Fact]
    public async Task CanListAllActiveForms()
    {
        // Create two forms
        await _client.PostAsJsonAsync("/api/evaluationforms", BuildCreateDto("List Form 1"));
        await _client.PostAsJsonAsync("/api/evaluationforms", BuildCreateDto("List Form 2"));

        var response = await _client.GetAsync("/api/evaluationforms");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var forms = await response.Content.ReadFromJsonAsync<List<EvaluationFormDto>>();
        Assert.NotNull(forms);
        Assert.True(forms.Count >= 2);
        Assert.All(forms, f => Assert.True(f.IsActive));
    }

    [Fact]
    public async Task CanUpdateForm()
    {
        var dto = BuildCreateDto("Update Test Form");
        var createResponse = await _client.PostAsJsonAsync("/api/evaluationforms", dto);
        var created = await createResponse.Content.ReadFromJsonAsync<EvaluationFormDto>();
        Assert.NotNull(created);

        var updateDto = new UpdateEvaluationFormDto
        {
            Name = "Updated Form Name",
            Description = "Updated description",
            IsActive = true
        };

        var updateResponse = await _client.PutAsJsonAsync($"/api/evaluationforms/{created.Id}", updateDto);
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/api/evaluationforms/{created.Id}");
        var updated = await getResponse.Content.ReadFromJsonAsync<EvaluationFormDto>();
        Assert.NotNull(updated);
        Assert.Equal("Updated Form Name", updated.Name);
    }

    [Fact]
    public async Task CanSoftDeleteForm()
    {
        var dto = BuildCreateDto("Delete Test Form");
        var createResponse = await _client.PostAsJsonAsync("/api/evaluationforms", dto);
        var created = await createResponse.Content.ReadFromJsonAsync<EvaluationFormDto>();
        Assert.NotNull(created);

        var deleteResponse = await _client.DeleteAsync($"/api/evaluationforms/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Soft-deleted form should still be retrievable by id but not in active list
        var getResponse = await _client.GetAsync($"/api/evaluationforms/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var form = await getResponse.Content.ReadFromJsonAsync<EvaluationFormDto>();
        Assert.NotNull(form);
        Assert.False(form.IsActive);

        var listResponse = await _client.GetAsync("/api/evaluationforms");
        var forms = await listResponse.Content.ReadFromJsonAsync<List<EvaluationFormDto>>();
        Assert.NotNull(forms);
        Assert.DoesNotContain(forms, f => f.Id == created.Id);
    }

    [Fact]
    public async Task GetNonExistentFormReturns404()
    {
        var response = await _client.GetAsync("/api/evaluationforms/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
