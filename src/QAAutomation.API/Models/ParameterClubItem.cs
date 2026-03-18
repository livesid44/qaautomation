namespace QAAutomation.API.Models;
public class ParameterClubItem {
    public int Id { get; set; }
    public int ClubId { get; set; }
    public ParameterClub Club { get; set; } = null!;
    public int ParameterId { get; set; }
    public Parameter Parameter { get; set; } = null!;
    public int Order { get; set; }
    public double? WeightOverride { get; set; }
    public int? RatingCriteriaId { get; set; }
    public RatingCriteria? RatingCriteria { get; set; }
}
