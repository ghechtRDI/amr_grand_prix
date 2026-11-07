using AmrGrandPrix.API.Models;

namespace AmrGrandPrix.API.Services.ResultsProcessing;

/// <summary>
/// Who may hold a course record (<see cref="RaceResult.IsNewRecord"/>): only the winner of their
/// gender in that race (one year's running of one <see cref="RaceVariant"/>).
/// </summary>
public static class CourseRecordRules
{
    /// <summary>
    /// Result IDs of each gender's winner among <paramref name="raceResults"/> (all results of one
    /// race): the fastest finished time, ties going to the lower place. Ranked by time, the same
    /// way <see cref="GrandPrix.GrandPrixCalculationService"/> ranks finishers for points.
    /// </summary>
    public static HashSet<Guid> GenderWinnerIds(IEnumerable<RaceResult> raceResults) =>
        raceResults
            .Where(r => r.Status == ResultStatus.Finished && r.Time.HasValue && r.Time > TimeSpan.Zero)
            .GroupBy(r => r.Gender)
            .Select(g => g.OrderBy(r => r.Time).ThenBy(r => r.Place ?? int.MaxValue).First().ResultId)
            .ToHashSet();

    /// <summary>
    /// Clears the course-record flag on any result in <paramref name="raceResults"/> that is no
    /// longer its gender's winner (e.g. a faster result was added or edited in). Returns them.
    /// </summary>
    public static List<RaceResult> ClearNonWinnerRecords(IReadOnlyCollection<RaceResult> raceResults)
    {
        var winners = GenderWinnerIds(raceResults);
        var cleared = raceResults.Where(r => r.IsNewRecord && !winners.Contains(r.ResultId)).ToList();
        foreach (var result in cleared)
            result.IsNewRecord = false;
        return cleared;
    }

    public static string GenderWord(Gender gender) => gender switch
    {
        Gender.Male => "men's",
        Gender.Female => "women's",
        _ => "nonbinary",
    };
}
