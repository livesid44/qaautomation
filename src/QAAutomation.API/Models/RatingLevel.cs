using System.ComponentModel.DataAnnotations;
namespace QAAutomation.API.Models;
public class RatingLevel {
    public int Id { get; set; }
    public int CriteriaId { get; set; }
    public RatingCriteria Criteria { get; set; } = null!;
    public int Score { get; set; }
    [Required] public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#6c757d";
}
