using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QAAutomation.API.Data;
using QAAutomation.API.DTOs;
using QAAutomation.API.Models;

namespace QAAutomation.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ParameterClubsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ParameterClubsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ParameterClubDto>>> GetAll()
    {
        var clubs = await _db.ParameterClubs
            .Include(c => c.Items)
                .ThenInclude(i => i.Parameter)
            .Include(c => c.Items)
                .ThenInclude(i => i.RatingCriteria)
            .ToListAsync();
        return Ok(clubs.Select(MapToDto));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ParameterClubDto>> GetById(int id)
    {
        var club = await _db.ParameterClubs
            .Include(c => c.Items)
                .ThenInclude(i => i.Parameter)
            .Include(c => c.Items)
                .ThenInclude(i => i.RatingCriteria)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (club is null) return NotFound();
        return Ok(MapToDto(club));
    }

    [HttpPost]
    public async Task<ActionResult<ParameterClubDto>> Create([FromBody] CreateParameterClubDto dto)
    {
        var club = new ParameterClub
        {
            Name = dto.Name,
            Description = dto.Description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.ParameterClubs.Add(club);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = club.Id }, MapToDto(club));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateParameterClubDto dto)
    {
        var club = await _db.ParameterClubs.FindAsync(id);
        if (club is null) return NotFound();
        club.Name = dto.Name;
        club.Description = dto.Description;
        club.IsActive = dto.IsActive;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id}/items")]
    public async Task<IActionResult> UpdateItems(int id, [FromBody] List<UpdateClubItemDto> items)
    {
        var club = await _db.ParameterClubs
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (club is null) return NotFound();

        // Remove existing items
        _db.ParameterClubItems.RemoveRange(club.Items);

        // Add new items
        var newItems = items.Select((item, idx) => new ParameterClubItem
        {
            ClubId = id,
            ParameterId = item.ParameterId,
            Order = item.Order == 0 ? idx : item.Order,
            WeightOverride = item.WeightOverride,
            RatingCriteriaId = item.RatingCriteriaId
        }).ToList();

        await _db.ParameterClubItems.AddRangeAsync(newItems);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var club = await _db.ParameterClubs.FindAsync(id);
        if (club is null) return NotFound();
        club.IsActive = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static ParameterClubDto MapToDto(ParameterClub c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Description = c.Description,
        IsActive = c.IsActive,
        CreatedAt = c.CreatedAt,
        Items = c.Items.OrderBy(i => i.Order).Select(i => new ParameterClubItemDto
        {
            Id = i.Id,
            ClubId = i.ClubId,
            ParameterId = i.ParameterId,
            ParameterName = i.Parameter?.Name ?? string.Empty,
            Order = i.Order,
            WeightOverride = i.WeightOverride,
            RatingCriteriaId = i.RatingCriteriaId,
            RatingCriteriaName = i.RatingCriteria?.Name
        }).ToList()
    };
}
