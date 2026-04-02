using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QAAutomation.API.Data;
using QAAutomation.API.DTOs;
using QAAutomation.API.Models;
using QAAutomation.API.Services;

namespace QAAutomation.API.Controllers;

/// <summary>Controller for managing evaluation results.</summary>
[ApiController]
[Route("api/[controller]")]
public class EvaluationResultsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TniGenerationService _tniGenerator;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EvaluationResultsController> _logger;

    public EvaluationResultsController(
        AppDbContext db,
        TniGenerationService tniGenerator,
        IServiceScopeFactory scopeFactory,
        ILogger<EvaluationResultsController> logger)
    {
        _db = db;
        _tniGenerator = tniGenerator;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>Gets all evaluation results. Filter by ?projectId=N to restrict to a single project.</summary>
    /// <returns>A list of evaluation results.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EvaluationResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EvaluationResultDto>>> GetAll([FromQuery] int? projectId = null)
    {
        var query = _db.EvaluationResults
            .Include(r => r.Form)
                .ThenInclude(f => f.Lob)
            .Include(r => r.Scores)
                .ThenInclude(s => s.Field)
                    .ThenInclude(f => f.Section)
            .AsQueryable();

        if (projectId.HasValue)
            query = query.Where(r => r.Form.Lob != null && r.Form.Lob.ProjectId == projectId.Value);

        var results = await query.ToListAsync();
        return Ok(results.Select(MapToDto));
    }

    /// <summary>Gets a single evaluation result by id.</summary>
    /// <param name="id">The result id.</param>
    /// <returns>The evaluation result with its scores.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(EvaluationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EvaluationResultDto>> GetById(int id)
    {
        var result = await _db.EvaluationResults
            .Include(r => r.Form)
            .Include(r => r.Scores)
                .ThenInclude(s => s.Field)
                    .ThenInclude(f => f.Section)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (result is null)
            return NotFound();

        return Ok(MapToDto(result));
    }

    /// <summary>Submits a new evaluation result with scores.</summary>
    /// <param name="dto">The evaluation result data.</param>
    /// <returns>The created evaluation result.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(EvaluationResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EvaluationResultDto>> Create([FromBody] CreateEvaluationResultDto dto)
    {
        var formExists = await _db.EvaluationForms.AnyAsync(f => f.Id == dto.FormId && f.IsActive);
        if (!formExists)
            return NotFound($"Active form with id {dto.FormId} not found.");

        var result = new EvaluationResult
        {
            FormId = dto.FormId,
            EvaluatedBy = dto.EvaluatedBy,
            EvaluatedAt = DateTime.UtcNow,
            Notes = dto.Notes,
            AgentName = dto.AgentName,
            CallReference = dto.CallReference,
            CallDate = dto.CallDate,
            OverallReasoning = dto.OverallReasoning,
            SentimentJson = dto.SentimentJson,
            FieldReasoningJson = dto.FieldReasoningJson,
            Scores = dto.Scores.Select(s => new EvaluationScore
            {
                FieldId = s.FieldId,
                Value = s.Value,
                NumericValue = s.NumericValue
            }).ToList()
        };

        _db.EvaluationResults.Add(result);
        await _db.SaveChangesAsync();

        // Reload with navigation properties
        await _db.Entry(result).Reference(r => r.Form).Query()
            .Include(f => f.Lob)
            .LoadAsync();
        await _db.Entry(result).Collection(r => r.Scores).Query()
            .Include(s => s.Field)
                .ThenInclude(f => f.Section)
            .LoadAsync();

        // ── Auto-trigger LLM-powered TNI for below-threshold parameters ───────
        // A field is considered "marked down" when its score is below the maximum:
        //   binary (MaxRating=1) → score is 0 (FAIL)
        //   rating (MaxRating>1) → score < MaxRating (any below-maximum score)
        // This matches the web-layer trigger so both paths create TNI consistently.
        var failedFields = result.Scores
            .Where(s => s.Field != null && s.NumericValue.HasValue &&
                        s.Field.MaxRating > 0 &&
                        s.NumericValue.Value < s.Field.MaxRating)
            .ToList();

        if (failedFields.Count > 0 && !string.IsNullOrWhiteSpace(dto.AgentName))
        {
            try
            {
                var targetAreas = failedFields
                    .Select(s => s.Field!.Label)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Distinct()
                    .ToList();

                var plan = new TrainingPlan
                {
                    Title = $"TNI – {dto.AgentName} ({DateTime.UtcNow:yyyy-MM-dd})",
                    Description = $"Auto-generated training plan from evaluation on {DateTime.UtcNow:yyyy-MM-dd}. " +
                                  $"Areas requiring improvement: {string.Join(", ", targetAreas)}.",
                    AgentName = dto.AgentName,
                    AgentUsername = dto.AgentUsername,
                    TrainerName = dto.EvaluatedBy,
                    Status = "Draft",
                    ProjectId = result.Form?.Lob?.ProjectId,
                    EvaluationResultId = result.Id,
                    CreatedBy = dto.EvaluatedBy,
                    IsAutoGenerated = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Items = failedFields.Select((s, i) => new TrainingPlanItem
                    {
                        TargetArea = s.Field!.Label,
                        ItemType = "Observation",
                        Content = $"Scored {s.NumericValue} / {s.Field.MaxRating} on \"{s.Field.Label}\". Improvement required.",
                        Order = i,
                        Status = "Pending"
                    }).ToList()
                };

                _db.TrainingPlans.Add(plan);
                await _db.SaveChangesAsync();

                // Fire-and-forget LLM content generation — uses its own DI scope so the
                // scoped DbContext stays valid after the current HTTP request scope ends.
                // Uses CancellationToken.None so the task isn't cancelled when the response
                // is sent (the HTTP request's cancellation token fires on response completion).
                var planId = plan.Id;
                var agentName = dto.AgentName;
                var formName = result.Form?.Name ?? "QA Form";
                _ = Task.Run(async () =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var generator = scope.ServiceProvider.GetRequiredService<TniGenerationService>();
                    var bgLogger = scope.ServiceProvider.GetRequiredService<ILogger<EvaluationResultsController>>();
                    try
                    {
                        var (content, json) = await generator.GenerateAsync(
                            targetAreas, agentName, formName);

                        var bgPlan = await db.TrainingPlans.FindAsync(planId);
                        if (bgPlan != null)
                        {
                            bgPlan.LlmTrainingContent = content;
                            bgPlan.AssessmentJson = json;
                            bgPlan.ContentGeneratedAt = DateTime.UtcNow;
                            bgPlan.UpdatedAt = DateTime.UtcNow;
                            await db.SaveChangesAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        bgLogger.LogWarning(ex,
                            "Auto-TNI LLM generation failed for plan {PlanId} (evaluation result {ResultId})",
                            planId, result.Id);
                    }
                });
            }
            catch (Exception ex)
            {
                // TNI creation must never break evaluation result creation — log and continue.
                _logger.LogWarning(ex,
                    "Auto-TNI plan creation failed for evaluation result {ResultId}; continuing without TNI.",
                    result.Id);
            }
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, MapToDto(result));
    }

    /// <summary>Gets all evaluation results for a specific form.</summary>
    /// <param name="formId">The form id.</param>
    /// <returns>A list of evaluation results for the specified form.</returns>
    [HttpGet("byform/{formId}")]
    [ProducesResponseType(typeof(IEnumerable<EvaluationResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EvaluationResultDto>>> GetByForm(int formId)
    {
        var results = await _db.EvaluationResults
            .Where(r => r.FormId == formId)
            .Include(r => r.Form)
            .Include(r => r.Scores)
                .ThenInclude(s => s.Field)
                    .ThenInclude(f => f.Section)
            .ToListAsync();

        return Ok(results.Select(MapToDto));
    }

    /// <summary>Deletes an evaluation result by id.</summary>
    /// <param name="id">The result id.</param>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _db.EvaluationResults.FindAsync(id);
        if (result is null) return NotFound();
        _db.EvaluationResults.Remove(result);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static EvaluationResultDto MapToDto(EvaluationResult result)
    {
        var method = result.Form?.ScoringMethod ?? ScoringMethod.Generic;
        var entries = result.Scores
            .Where(s => s.Field?.Section != null && s.Field.FieldType == FieldType.Rating)
            .Select(s => new ScoringCalculator.FieldEntry(
                s.Field.Section.Id,
                s.Field.Section.Title,
                s.Field.Section.Order,
                s.NumericValue ?? 0,
                s.Field.MaxRating));
        var (totalScore, maxScore, sections) = ScoringCalculator.Compute(method, entries);

        return new EvaluationResultDto
        {
            Id = result.Id,
            FormId = result.FormId,
            FormName = result.Form?.Name ?? string.Empty,
            EvaluatedBy = result.EvaluatedBy,
            EvaluatedAt = result.EvaluatedAt,
            Notes = result.Notes,
            AgentName = result.AgentName,
            CallReference = result.CallReference,
            CallDate = result.CallDate,
            CallDurationSeconds = result.CallDurationSeconds,
            OverallReasoning = result.OverallReasoning,
            SentimentJson = result.SentimentJson,
            FieldReasoningJson = result.FieldReasoningJson,
            Scores = result.Scores.Select(s => new EvaluationScoreDto
            {
                Id = s.Id,
                ResultId = s.ResultId,
                FieldId = s.FieldId,
                Value = s.Value,
                NumericValue = s.NumericValue
            }).ToList(),
            TotalScore = totalScore,
            MaxPossibleScore = maxScore,
            Sections = sections.Select(sec =>
            {
                var rawFields = result.Scores
                    .Where(s => s.Field?.Section?.Title == sec.SectionTitle && s.Field.Section.Order == sec.SectionOrder)
                    .OrderBy(s => s.Field!.Order);
                return new EvaluationResultSectionDto
                {
                    Title = sec.SectionTitle,
                    SectionScore = sec.Score,
                    SectionMax = sec.MaxScore,
                    Fields = rawFields.Select(s => new EvaluationResultFieldScoreDto
                    {
                        FieldId = s.FieldId,
                        FieldLabel = s.Field!.Label,
                        MaxRating = s.Field.MaxRating,
                        Value = s.Value,
                        NumericValue = s.NumericValue
                    }).ToList()
                };
            }).ToList()
        };
    }
}
