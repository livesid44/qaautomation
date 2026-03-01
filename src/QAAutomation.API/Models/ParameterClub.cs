using System.ComponentModel.DataAnnotations;
namespace QAAutomation.API.Models;
public class ParameterClub {
    public int Id { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public ICollection<ParameterClubItem> Items { get; set; } = new List<ParameterClubItem>();
}
