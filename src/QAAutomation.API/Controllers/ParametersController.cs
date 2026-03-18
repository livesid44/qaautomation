using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QAAutomation.API.Data;
using QAAutomation.API.DTOs;
using QAAutomation.API.Models;

namespace QAAutomation.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ParametersController : ControllerBase
{
    private readonly AppDbContext _db;

    public ParametersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ParameterDto>>> GetAll([FromQuery] int? projectId = null)
    {
        var query = _db.Parameters.AsQueryable();
        if (projectId.HasValue) query = query.Where(p => p.ProjectId == projectId.Value);
        var items = await query.ToListAsync();
        return Ok(items.Select(MapToDto));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ParameterDto>> GetById(int id)
    {
        var item = await _db.Parameters.FindAsync(id);
        if (item is null) return NotFound();
        return Ok(MapToDto(item));
    }

    [HttpPost]
    public async Task<ActionResult<ParameterDto>> Create([FromBody] CreateParameterDto dto)
    {
        var item = new Parameter
        {
            Name = dto.Name,
            Description = dto.Description,
            Category = dto.Category,
            DefaultWeight = dto.DefaultWeight,
            EvaluationType = dto.EvaluationType,
            ProjectId = dto.ProjectId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.Parameters.Add(item);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, MapToDto(item));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateParameterDto dto)
    {
        var item = await _db.Parameters.FindAsync(id);
        if (item is null) return NotFound();
        item.Name = dto.Name;
        item.Description = dto.Description;
        item.Category = dto.Category;
        item.DefaultWeight = dto.DefaultWeight;
        item.EvaluationType = dto.EvaluationType;
        item.IsActive = dto.IsActive;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.Parameters.FindAsync(id);
        if (item is null) return NotFound();
        item.IsActive = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static ParameterDto MapToDto(Parameter p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        Category = p.Category,
        DefaultWeight = p.DefaultWeight,
        IsActive = p.IsActive,
        CreatedAt = p.CreatedAt,
        EvaluationType = p.EvaluationType,
        ProjectId = p.ProjectId
    };
}
