using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QAAutomation.API.Data;
using QAAutomation.API.DTOs;
using QAAutomation.API.Models;

namespace QAAutomation.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAll()
    {
        var users = await _db.AppUsers.ToListAsync();
        return Ok(users.Select(MapToDto));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetById(int id)
    {
        var user = await _db.AppUsers.FindAsync(id);
        if (user is null) return NotFound();
        return Ok(MapToDto(user));
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserDto dto)
    {
        if (await _db.AppUsers.AnyAsync(u => u.Username == dto.Username))
            return Conflict(new { message = "Username already exists" });

        var user = new AppUser
        {
            Username = dto.Username,
            PasswordHash = AppDbContext.HashPassword(dto.Password),
            Email = dto.Email,
            Role = dto.Role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.AppUsers.Add(user);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, MapToDto(user));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserDto dto)
    {
        var user = await _db.AppUsers.FindAsync(id);
        if (user is null) return NotFound();
        user.Email = dto.Email;
        user.Role = dto.Role;
        user.IsActive = dto.IsActive;
        if (!string.IsNullOrEmpty(dto.Password))
            user.PasswordHash = AppDbContext.HashPassword(dto.Password);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.AppUsers.FindAsync(id);
        if (user is null) return NotFound();
        _db.AppUsers.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static UserDto MapToDto(AppUser u) => new()
    {
        Id = u.Id,
        Username = u.Username,
        Email = u.Email,
        Role = u.Role,
        IsActive = u.IsActive,
        CreatedAt = u.CreatedAt
    };
}
