using System.ComponentModel.DataAnnotations;
namespace QAAutomation.API.Models;
public class RatingCriteria {
    public int Id { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int MinScore { get; set; } = 1;
    public int MaxScore { get; set; } = 5;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }
    public ICollection<RatingLevel> Levels { get; set; } = new List<RatingLevel>();
}
