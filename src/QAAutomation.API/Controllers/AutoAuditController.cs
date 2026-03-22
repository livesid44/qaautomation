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
            .Include(f => f.Lob)
            .FirstOrDefaultAsync(f => f.Id == request.FormId && f.IsActive);

        if (form == null)
            return NotFound($"Active evaluation form with id {request.FormId} not found.");

        // Load parameter metadata (label → EvaluationType + Description) for LLM prompt enrichment
        var paramMap = await _db.Parameters
            .Where(p => p.IsActive)
            .Select(p => new { p.Name, p.EvaluationType, p.Description })
            .ToDictionaryAsync(p => p.Name, p => (p.EvaluationType, p.Description));

        var fieldDefinitions = form.Sections
            .OrderBy(s => s.Order)
            .SelectMany(s => s.Fields
                .OrderBy(f => f.Order)
                .Select(f =>
                {
                    var (evalType, desc) = paramMap.TryGetValue(f.Label, out var pm) ? pm : ("LLM", (string?)null);
                    return new AutoAuditFieldDefinition(
                        FieldId: f.Id,
                        Label: f.Label,
                        Description: desc,
                        MaxRating: f.MaxRating,
                        IsRequired: f.IsRequired,
                        SectionTitle: s.Title,
                        EvaluationType: evalType);
                }))
            .ToList();

        if (fieldDefinitions.Count == 0)
            return BadRequest("The selected form has no fields to score.");

        // ── PII / SPII protection (tenant-level) ─────────────────────────────
        // Resolve the project for this form and apply PII protection if configured.
        if (form.Lob?.ProjectId is int projectId)
        {
            var project = await _db.Projects.FindAsync(projectId);
            if (project is { PiiProtectionEnabled: true })
            {
                if (project.PiiRedactionMode == "Block")
                {
                    // Hard block: refuse the request when any PII is detected
                    if (PiiRedactionService.ContainsPii(request.Transcript))
                    {
                        var types = PiiRedactionService.DetectTypes(request.Transcript);
                        return BadRequest(
                            $"PII/SPII protection is enabled for this project with mode 'Block'. " +
                            $"Detected sensitive data type(s): {string.Join(", ", types)}. " +
                            "Please remove PII from the transcript before submitting for AI analysis.");
                    }
                }
                else // "Redact" (default)
                {
                    // Soft redact: replace PII tokens with labelled placeholders
                    request.Transcript = PiiRedactionService.Redact(request.Transcript);
                }
            }
        }

        var result = await _auditService.AnalyzeTranscriptAsync(
            request, fieldDefinitions, form.Name, form.Lob?.ProjectId, HttpContext.RequestAborted);

        return Ok(result);
    }
}
