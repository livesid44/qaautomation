using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QAAutomation.API.Data;
using QAAutomation.API.DTOs;
using QAAutomation.API.Models;

namespace QAAutomation.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProjectsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProjectDto>>> GetAll()
    {
        var projects = await _db.Projects
            .Include(p => p.Lobs)
            .ToListAsync();
        return Ok(projects.Select(p => new ProjectDto
        {
            Id = p.Id, Name = p.Name, Description = p.Description,
            IsActive = p.IsActive, CreatedAt = p.CreatedAt,
            LobCount = p.Lobs.Count(l => l.IsActive),
            PiiProtectionEnabled = p.PiiProtectionEnabled,
            PiiRedactionMode = p.PiiRedactionMode
        }));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProjectDto>> GetById(int id)
    {
        var p = await _db.Projects.Include(x => x.Lobs).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        return Ok(new ProjectDto
        {
            Id = p.Id, Name = p.Name, Description = p.Description,
            IsActive = p.IsActive, CreatedAt = p.CreatedAt,
            LobCount = p.Lobs.Count(l => l.IsActive),
            PiiProtectionEnabled = p.PiiProtectionEnabled,
            PiiRedactionMode = p.PiiRedactionMode
        });
    }

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create([FromBody] CreateProjectDto dto)
    {
        var project = new Project
        {
            Name = dto.Name, Description = dto.Description,
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = project.Id },
            new ProjectDto { Id = project.Id, Name = project.Name, Description = project.Description, IsActive = project.IsActive, CreatedAt = project.CreatedAt, PiiProtectionEnabled = project.PiiProtectionEnabled, PiiRedactionMode = project.PiiRedactionMode });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProjectDto dto)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project is null) return NotFound();
        project.Name = dto.Name;
        project.Description = dto.Description;
        project.IsActive = dto.IsActive;
        project.PiiProtectionEnabled = dto.PiiProtectionEnabled;
        project.PiiRedactionMode = string.IsNullOrWhiteSpace(dto.PiiRedactionMode) ? "Redact" : dto.PiiRedactionMode;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project is null) return NotFound();
        _db.Projects.Remove(project);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── User access management ────────────────────────────────────────────────

    /// <summary>Get all users with access to this project.</summary>
    [HttpGet("{id}/users")]
    public async Task<ActionResult<IEnumerable<object>>> GetUsers(int id)
    {
        var access = await _db.UserProjectAccesses
            .Where(a => a.ProjectId == id)
            .Include(a => a.User)
            .Select(a => new { a.User.Id, a.User.Username, a.User.Email, a.User.Role, a.GrantedAt })
            .ToListAsync();
        return Ok(access);
    }

    /// <summary>Grant a user access to this project.</summary>
    [HttpPost("{id}/users/{userId}")]
    public async Task<IActionResult> GrantAccess(int id, int userId)
    {
        if (await _db.UserProjectAccesses.AnyAsync(a => a.ProjectId == id && a.UserId == userId))
            return Conflict(new { message = "User already has access" });
        _db.UserProjectAccesses.Add(new UserProjectAccess { ProjectId = id, UserId = userId, GrantedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        return Ok();
    }

    /// <summary>Revoke a user's access to this project.</summary>
    [HttpDelete("{id}/users/{userId}")]
    public async Task<IActionResult> RevokeAccess(int id, int userId)
    {
        var access = await _db.UserProjectAccesses.FirstOrDefaultAsync(a => a.ProjectId == id && a.UserId == userId);
        if (access is null) return NotFound();
        _db.UserProjectAccesses.Remove(access);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
