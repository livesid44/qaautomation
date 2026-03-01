using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QAAutomation.API.Data;
using QAAutomation.API.DTOs;
using QAAutomation.API.Models;

namespace QAAutomation.API.Controllers;

/// <summary>Controller for managing evaluation results.</summary>
[ApiController]
[Route("api/[controller]")]
public class EvaluationResultsController : ControllerBase
{
    private readonly AppDbContext _db;

    public EvaluationResultsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Gets all evaluation results.</summary>
    /// <returns>A list of all evaluation results.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EvaluationResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EvaluationResultDto>>> GetAll()
    {
        var results = await _db.EvaluationResults
            .Include(r => r.Form)
            .Include(r => r.Scores)
                .ThenInclude(s => s.Field)
                    .ThenInclude(f => f.Section)
            .ToListAsync();

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
        await _db.Entry(result).Reference(r => r.Form).LoadAsync();
        await _db.Entry(result).Collection(r => r.Scores).Query()
            .Include(s => s.Field)
                .ThenInclude(f => f.Section)
            .LoadAsync();

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

    private static EvaluationResultDto MapToDto(EvaluationResult result) => new()
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
        Scores = result.Scores.Select(s => new EvaluationScoreDto
        {
            Id = s.Id,
            ResultId = s.ResultId,
            FieldId = s.FieldId,
            Value = s.Value,
            NumericValue = s.NumericValue
        }).ToList(),
        TotalScore = result.TotalScore,
        MaxPossibleScore = result.MaxPossibleScore,
        Sections = result.Scores
            .Where(s => s.Field?.Section != null)
            .GroupBy(s => new { s.Field.Section.Id, s.Field.Section.Title, s.Field.Section.Order })
            .OrderBy(g => g.Key.Order)
            .Select(g => new EvaluationResultSectionDto
            {
                Title = g.Key.Title,
                Fields = g.OrderBy(s => s.Field.Order).Select(s => new EvaluationResultFieldScoreDto
                {
                    FieldId = s.FieldId,
                    FieldLabel = s.Field.Label,
                    MaxRating = s.Field.MaxRating,
                    Value = s.Value,
                    NumericValue = s.NumericValue
                }).ToList()
            }).ToList()
    };
}
