namespace QAAutomation.API.DTOs;

public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateUserDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = "User";
}

public class UpdateUserDto
{
    public string? Email { get; set; }
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; }
    public string? Password { get; set; }
}
