using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QAAutomation.API.Data;
using QAAutomation.API.DTOs;

namespace QAAutomation.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuthController(AppDbContext db) => _db = db;

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto dto)
    {
        var hash = AppDbContext.HashPassword(dto.Password);
        var user = await _db.AppUsers
            .FirstOrDefaultAsync(u => u.Username == dto.Username && u.PasswordHash == hash && u.IsActive);

        if (user is null)
            return Ok(new LoginResponseDto { Success = false, Message = "Invalid username or password" });

        // Admins get all active projects; regular users get only their assigned projects
        List<ProjectDto> projects;
        if (user.Role == "Admin")
        {
            projects = await _db.Projects
                .Where(p => p.IsActive)
                .Select(p => new ProjectDto
                {
                    Id = p.Id, Name = p.Name, Description = p.Description,
                    IsActive = p.IsActive, CreatedAt = p.CreatedAt,
                    LobCount = p.Lobs.Count(l => l.IsActive)
                })
                .ToListAsync();
        }
        else
        {
            projects = await _db.UserProjectAccesses
                .Where(a => a.UserId == user.Id && a.Project.IsActive)
                .Select(a => new ProjectDto
                {
                    Id = a.Project.Id, Name = a.Project.Name, Description = a.Project.Description,
                    IsActive = a.Project.IsActive, CreatedAt = a.Project.CreatedAt,
                    LobCount = a.Project.Lobs.Count(l => l.IsActive)
                })
                .ToListAsync();
        }

        return Ok(new LoginResponseDto
        {
            Success = true,
            Username = user.Username,
            Role = user.Role,
            Message = "Login successful",
            Projects = projects
        });
    }
}
