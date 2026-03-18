using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QAAutomation.API.Data;
using QAAutomation.API.DTOs;
using QAAutomation.API.Models;

namespace QAAutomation.API.Controllers;

/// <summary>Controller for managing evaluation forms.</summary>
[ApiController]
[Route("api/[controller]")]
public class EvaluationFormsController : ControllerBase
{
    private readonly AppDbContext _db;

    public EvaluationFormsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Gets all active evaluation forms. Filter by ?projectId=N or ?lobId=N.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EvaluationFormDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EvaluationFormDto>>> GetAll([FromQuery] int? projectId = null, [FromQuery] int? lobId = null)
    {
        var query = _db.EvaluationForms
            .Where(f => f.IsActive)
            .Include(f => f.Sections).ThenInclude(s => s.Fields)
            .Include(f => f.Lob).ThenInclude(l => l.Project)
            .AsQueryable();
        if (lobId.HasValue) query = query.Where(f => f.LobId == lobId.Value);
        else if (projectId.HasValue) query = query.Where(f => f.Lob != null && f.Lob.ProjectId == projectId.Value);
        var forms = await query.ToListAsync();
        return Ok(forms.Select(MapToDto));
    }

    /// <summary>Gets a single evaluation form by id.</summary>
    /// <param name="id">The form id.</param>
    /// <returns>The evaluation form with its sections and fields.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(EvaluationFormDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EvaluationFormDto>> GetById(int id)
    {
        var form = await _db.EvaluationForms
            .Include(f => f.Sections)
                .ThenInclude(s => s.Fields)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (form is null)
            return NotFound();

        return Ok(MapToDto(form));
    }

    /// <summary>Creates a new evaluation form with sections and fields.</summary>
    /// <param name="dto">The form data.</param>
    /// <returns>The created evaluation form.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(EvaluationFormDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EvaluationFormDto>> Create([FromBody] CreateEvaluationFormDto dto)
    {
        var form = new EvaluationForm
        {
            Name = dto.Name,
            Description = dto.Description,
            LobId = dto.LobId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true,
            Sections = dto.Sections.Select(s => new FormSection
            {
                Title = s.Title,
                Description = s.Description,
                Order = s.Order,
                Fields = s.Fields.Select(f => new FormField
                {
                    Label = f.Label,
                    FieldType = f.FieldType,
                    IsRequired = f.IsRequired,
                    Order = f.Order,
                    Options = f.Options,
                    MaxRating = f.MaxRating
                }).ToList()
            }).ToList()
        };

        _db.EvaluationForms.Add(form);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = form.Id }, MapToDto(form));
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateEvaluationFormDto dto)
    {
        var form = await _db.EvaluationForms.FindAsync(id);
        if (form is null) return NotFound();

        form.Name = dto.Name;
        form.Description = dto.Description;
        form.IsActive = dto.IsActive;
        form.LobId = dto.LobId;
        form.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Soft deletes an evaluation form by setting IsActive to false.</summary>
    /// <param name="id">The form id.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var form = await _db.EvaluationForms.FindAsync(id);
        if (form is null)
            return NotFound();

        form.IsActive = false;
        form.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static EvaluationFormDto MapToDto(EvaluationForm form) => new()
    {
        Id = form.Id,
        Name = form.Name,
        Description = form.Description,
        CreatedAt = form.CreatedAt,
        UpdatedAt = form.UpdatedAt,
        IsActive = form.IsActive,
        LobId = form.LobId,
        LobName = form.Lob?.Name,
        ProjectId = form.Lob?.ProjectId,
        ProjectName = form.Lob?.Project?.Name,
        Sections = form.Sections.OrderBy(s => s.Order).Select(s => new FormSectionDto
        {
            Id = s.Id,
            Title = s.Title,
            Description = s.Description,
            Order = s.Order,
            FormId = s.FormId,
            Fields = s.Fields.OrderBy(f => f.Order).Select(f => new FormFieldDto
            {
                Id = f.Id,
                Label = f.Label,
                Description = f.Description,
                FieldType = f.FieldType,
                IsRequired = f.IsRequired,
                Order = f.Order,
                Options = f.Options,
                MaxRating = f.MaxRating,
                SectionId = f.SectionId
            }).ToList()
        }).ToList()
    };
}
