using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QAAutomation.API.Data;
using QAAutomation.API.DTOs;
using QAAutomation.API.Services;

namespace QAAutomation.API.Controllers;

/// <summary>Controller for automated QA audit using LLM transcript analysis.</summary>
[ApiController]
[Route("api/[controller]")]
public class AutoAuditController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAutoAuditService _auditService;

    public AutoAuditController(AppDbContext db, IAutoAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    /// <summary>
    /// Analyzes a call transcript against the specified evaluation form using LLM.
    /// Returns AI-suggested scores and reasoning for each field.
    /// </summary>
    /// <param name="request">The auto-audit request containing the transcript and form details.</param>
    /// <returns>AI-generated scores and reasoning for each evaluation field.</returns>
    [HttpPost("analyze")]
    [ProducesResponseType(typeof(AutoAuditResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AutoAuditResponseDto>> Analyze([FromBody] AutoAuditRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Transcript))
            return BadRequest("Transcript cannot be empty.");

        var form = await _db.EvaluationForms
            .Include(f => f.Sections)
                .ThenInclude(s => s.Fields)
            .FirstOrDefaultAsync(f => f.Id == request.FormId && f.IsActive);

        if (form == null)
            return NotFound($"Active evaluation form with id {request.FormId} not found.");

        var fieldDefinitions = form.Sections
            .OrderBy(s => s.Order)
            .SelectMany(s => s.Fields
                .OrderBy(f => f.Order)
                .Select(f => new AutoAuditFieldDefinition(
                    FieldId: f.Id,
                    Label: f.Label,
                    Description: null,
                    MaxRating: f.MaxRating,
                    IsRequired: f.IsRequired,
                    SectionTitle: s.Title)))
            .ToList();

        if (fieldDefinitions.Count == 0)
            return BadRequest("The selected form has no fields to score.");

        var result = await _auditService.AnalyzeTranscriptAsync(
            request, fieldDefinitions, form.Name, HttpContext.RequestAborted);

        return Ok(result);
    }
}
