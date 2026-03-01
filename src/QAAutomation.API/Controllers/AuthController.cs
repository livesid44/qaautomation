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

        return Ok(new LoginResponseDto
        {
            Success = true,
            Username = user.Username,
            Role = user.Role,
            Message = "Login successful"
        });
    }
}
