using System.ComponentModel.DataAnnotations;

namespace QAAutomation.API.DTOs;

public class LoginRequestDto
{
    [Required] public string Username { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class LoginResponseDto
{
    public bool Success { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    /// <summary>Projects the user has access to. Admins always get all projects.</summary>
    public List<ProjectDto> Projects { get; set; } = new();
}
