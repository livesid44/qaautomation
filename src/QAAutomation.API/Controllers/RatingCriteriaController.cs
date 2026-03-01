using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QAAutomation.API.Data;
using QAAutomation.API.DTOs;
using QAAutomation.API.Models;

namespace QAAutomation.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RatingCriteriaController : ControllerBase
{
    private readonly AppDbContext _db;

    public RatingCriteriaController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RatingCriteriaDto>>> GetAll()
    {
        var items = await _db.RatingCriteria
            .Include(c => c.Levels)
            .ToListAsync();
        return Ok(items.Select(MapToDto));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<RatingCriteriaDto>> GetById(int id)
    {
        var item = await _db.RatingCriteria
            .Include(c => c.Levels)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (item is null) return NotFound();
        return Ok(MapToDto(item));
    }

    [HttpPost]
    public async Task<ActionResult<RatingCriteriaDto>> Create([FromBody] CreateRatingCriteriaDto dto)
    {
        var item = new RatingCriteria
        {
            Name = dto.Name,
            Description = dto.Description,
            MinScore = dto.MinScore,
            MaxScore = dto.MaxScore,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Levels = dto.Levels.Select(l => new RatingLevel
            {
                Score = l.Score,
                Label = l.Label,
                Description = l.Description,
                Color = l.Color
            }).ToList()
        };
        _db.RatingCriteria.Add(item);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, MapToDto(item));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRatingCriteriaDto dto)
    {
        var item = await _db.RatingCriteria
            .Include(c => c.Levels)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (item is null) return NotFound();

        item.Name = dto.Name;
        item.Description = dto.Description;
        item.MinScore = dto.MinScore;
        item.MaxScore = dto.MaxScore;
        item.IsActive = dto.IsActive;

        // Replace levels
        _db.RatingLevels.RemoveRange(item.Levels);
        item.Levels = dto.Levels.Select(l => new RatingLevel
        {
            CriteriaId = id,
            Score = l.Score,
            Label = l.Label,
            Description = l.Description,
            Color = l.Color
        }).ToList();

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.RatingCriteria.FindAsync(id);
        if (item is null) return NotFound();
        item.IsActive = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static RatingCriteriaDto MapToDto(RatingCriteria c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Description = c.Description,
        MinScore = c.MinScore,
        MaxScore = c.MaxScore,
        IsActive = c.IsActive,
        CreatedAt = c.CreatedAt,
        Levels = c.Levels.OrderBy(l => l.Score).Select(l => new RatingLevelDto
        {
            Id = l.Id,
            CriteriaId = l.CriteriaId,
            Score = l.Score,
            Label = l.Label,
            Description = l.Description,
            Color = l.Color
        }).ToList()
    };
}
