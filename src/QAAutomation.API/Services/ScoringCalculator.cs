using QAAutomation.API.Models;

namespace QAAutomation.API.Services;

/// <summary>
/// Centralised scoring logic for evaluation forms.
///
/// Two modes are supported:
/// <list type="bullet">
///   <item><see cref="ScoringMethod.Generic"/> — standard proportional sum across all fields.</item>
///   <item><see cref="ScoringMethod.SectionAutoFail"/> — if any field in a section scores 0 the
///     entire section contributes 0 to the total (YouTube IQA behaviour).</item>
/// </list>
/// </summary>
public static class ScoringCalculator
{
    /// <summary>
    /// Input record representing a single scored field with its section context.
    /// </summary>
    public record FieldEntry(
        int SectionId,
        string SectionTitle,
        int SectionOrder,
        double Score,
        double MaxScore);

    /// <summary>
    /// Computed scores for a single section.
    /// </summary>
    /// <param name="SectionId">DB id of the section.</param>
    /// <param name="SectionTitle">Display title of the section.</param>
    /// <param name="SectionOrder">Display order of the section.</param>
    /// <param name="Score">Effective section score (0 when <see cref="ScoringMethod.SectionAutoFail"/> triggers).</param>
    /// <param name="MaxScore">Maximum possible score for this section.</param>
    public record SectionScore(
        int SectionId,
        string SectionTitle,
        int SectionOrder,
        double Score,
        double MaxScore)
    {
        public double ScorePercent => MaxScore > 0 ? Math.Round(Score / MaxScore * 100, 1) : 0;
    }

    /// <summary>
    /// Computes the total score and per-section breakdown.
    /// </summary>
    /// <param name="method">Scoring method to apply.</param>
    /// <param name="fields">Flat list of scored field entries grouped into sections.</param>
    /// <returns>
    /// A tuple of effective total score, maximum possible score, and per-section breakdown.
    /// </returns>
    public static (double TotalScore, double MaxScore, IReadOnlyList<SectionScore> Sections)
        Compute(ScoringMethod method, IEnumerable<FieldEntry> fields)
    {
        var sections = fields
            .GroupBy(f => new { f.SectionId, f.SectionTitle, f.SectionOrder })
            .OrderBy(g => g.Key.SectionOrder)
            .Select(g =>
            {
                var rawScore = g.Sum(f => f.Score);
                var maxScore = g.Sum(f => f.MaxScore);
                // SectionAutoFail: any field scoring 0 → entire section becomes 0
                var effectiveScore = method == ScoringMethod.SectionAutoFail && g.Any(f => f.Score == 0)
                    ? 0
                    : rawScore;
                return new SectionScore(g.Key.SectionId, g.Key.SectionTitle, g.Key.SectionOrder, effectiveScore, maxScore);
            })
            .ToList();

        var totalScore = sections.Sum(s => s.Score);
        var maxScore = sections.Sum(s => s.MaxScore);
        return (totalScore, maxScore, sections);
    }

    /// <summary>
    /// Computes the score percentage. Returns 0 when <paramref name="maxScore"/> is 0.
    /// </summary>
    public static double ScorePercent(double totalScore, double maxScore) =>
        maxScore > 0 ? Math.Round(totalScore / maxScore * 100, 1) : 0;
}
