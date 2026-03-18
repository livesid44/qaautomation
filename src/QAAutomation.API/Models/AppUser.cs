using System.ComponentModel.DataAnnotations;
namespace QAAutomation.API.Models;
public class AppUser {
    public int Id { get; set; }
    [Required] public string Username { get; set; } = string.Empty;
    [Required] public string PasswordHash { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
