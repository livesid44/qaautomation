using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QAAutomation.API.Data;
using QAAutomation.API.DTOs;
using QAAutomation.API.Models;

namespace QAAutomation.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LobsController : ControllerBase
{
    private readonly AppDbContext _db;
    public LobsController(AppDbContext db) => _db = db;

    /// <summary>Get all LOBs. Filter by project using ?projectId=N.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LobDto>>> GetAll([FromQuery] int? projectId = null)
    {
        var query = _db.Lobs.Include(l => l.Project).Include(l => l.EvaluationForms).AsQueryable();
        if (projectId.HasValue)
            query = query.Where(l => l.ProjectId == projectId.Value);
        var lobs = await query.ToListAsync();
        return Ok(lobs.Select(MapToDto));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<LobDto>> GetById(int id)
    {
        var lob = await _db.Lobs.Include(l => l.Project).Include(l => l.EvaluationForms).FirstOrDefaultAsync(l => l.Id == id);
        if (lob is null) return NotFound();
        return Ok(MapToDto(lob));
    }

    [HttpPost]
    public async Task<ActionResult<LobDto>> Create([FromBody] CreateLobDto dto)
    {
        var lob = new Lob
        {
            ProjectId = dto.ProjectId, Name = dto.Name,
            Description = dto.Description, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        _db.Lobs.Add(lob);
        await _db.SaveChangesAsync();
        await _db.Entry(lob).Reference(l => l.Project).LoadAsync();
        return CreatedAtAction(nameof(GetById), new { id = lob.Id }, MapToDto(lob));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateLobDto dto)
    {
        var lob = await _db.Lobs.FindAsync(id);
        if (lob is null) return NotFound();
        lob.Name = dto.Name;
        lob.Description = dto.Description;
        lob.IsActive = dto.IsActive;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var lob = await _db.Lobs.FindAsync(id);
        if (lob is null) return NotFound();
        _db.Lobs.Remove(lob);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static LobDto MapToDto(Lob l) => new()
    {
        Id = l.Id, ProjectId = l.ProjectId,
        ProjectName = l.Project?.Name ?? "",
        Name = l.Name, Description = l.Description,
        IsActive = l.IsActive, CreatedAt = l.CreatedAt,
        FormCount = l.EvaluationForms.Count
    };
}
