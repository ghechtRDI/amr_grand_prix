using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AmrGrandPrix.API.Services.GrandPrix;

/// <summary>
/// Service for calculating Grand Prix points and standings
/// </summary>
public class GrandPrixCalculationService : IGrandPrixCalculationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<GrandPrixCalculationService> _logger;

    private const int RecordBonusPoints = 10;
    private const int MinRacesForEligibility = 1; // Must finish top 20/5 in at least 1 race
    private const int BestRacesCount = 4; // Count best 4 races

    public GrandPrixCalculationService(
        ApplicationDbContext context,
        ILogger<GrandPrixCalculationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public int CalculateOpenDivisionPoints(int placeInGender, bool isNewRecord = false)
    {
        var basePoints = GrandPrixConstants.GetOpenDivisionPoints(placeInGender);
        var bonus = isNewRecord ? RecordBonusPoints : 0;
        return basePoints + bonus;
    }

    public int CalculateAgeDivisionPoints(int placeInAgeCategory)
    {
        return GrandPrixConstants.GetAgeDivisionPoints(placeInAgeCategory);
    }

    public string DetermineAgeCategory(int age)
    {
        return GrandPrixConstants.GetAgeCategory(age);
    }

    /// <summary>
    /// Age category for a result: uses the stored category directly (set when only a category
    /// was reported), falling back to deriving one from the exact age. Null when neither is known.
    /// </summary>
    private string? GetResultAgeCategory(RaceResult result) =>
        result.AgeCategory ?? (result.Age.HasValue ? DetermineAgeCategory(result.Age.Value) : null);

    public async Task<int> CalculateRacePointsAsync(Guid raceId)
    {
        _logger.LogInformation("Calculating points for race {RaceId}", raceId);

        var race = await _context.Races
            .Include(r => r.RaceVariant)
            .Include(r => r.Results)
            .ThenInclude(r => r.Runner)
            .FirstOrDefaultAsync(r => r.RaceId == raceId);

        if (race == null)
        {
            _logger.LogWarning("Race {RaceId} not found", raceId);
            return 0;
        }

        if (!await AffectsStandingsAsync(race))
            return 0;

        await EnsureSeasonNotFinalizedAsync(race.Year);

        // Delete existing points for this race (also clears stale points if the race was
        // un-flagged as a Grand Prix race)
        var existingPoints = await _context.GrandPrixPoints
            .Where(p => p.RaceId == raceId)
            .ToListAsync();

        _context.GrandPrixPoints.RemoveRange(existingPoints);
        await _context.SaveChangesAsync();

        if (!race.IsGrandPrixRace)
        {
            _logger.LogInformation("Race {RaceId} is not a Grand Prix race, skipping points calculation", raceId);
            return 0;
        }

        // Get finished results only
        var results = race.Results
            .Where(r => r.Status == ResultStatus.Finished && r.Time.HasValue)
            .OrderBy(r => r.Time)
            .ToList();

        var pointsCreated = 0;

        // Calculate place within gender - nonbinary finishers are ranked and scored only
        // against other nonbinary finishers, as their own division, same as Male/Female.
        var maleResults      = results.Where(r => r.Gender == Gender.Male).ToList();
        var femaleResults    = results.Where(r => r.Gender == Gender.Female).ToList();
        var nonbinaryResults = results.Where(r => r.Gender == Gender.Nonbinary).ToList();

        pointsCreated += await CalculateGenderDivisionPoints(race, maleResults, Gender.Male);
        pointsCreated += await CalculateGenderDivisionPoints(race, femaleResults, Gender.Female);
        pointsCreated += await CalculateGenderDivisionPoints(race, nonbinaryResults, Gender.Nonbinary);

        await _context.SaveChangesAsync();

        _logger.LogInformation("Created {Count} point records for race {RaceId}", pointsCreated, raceId);

        return pointsCreated;
    }

    private async Task<int> CalculateGenderDivisionPoints(Race race, List<RaceResult> results, Gender gender)
    {
        var pointsCreated = 0;

        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            var placeInGender = i + 1;

            result.PlaceGender = placeInGender;

            // Open Division points: top 20 finishers per gender, juniors included — except on a
            // junior-only course (e.g. Jack's Run), which scores age groups only
            if (placeInGender <= 20 && !race.RaceVariant.IsAgeGroupOnly)
            {
                var openPoints = CalculateOpenDivisionPoints(placeInGender, result.IsNewRecord);
                var openDivision = GrandPrixConstants.GetOpenDivision(gender);

                _context.GrandPrixPoints.Add(new GrandPrixPoints
                {
                    PointsId = Guid.NewGuid(),
                    RunnerId = result.RunnerId,
                    RaceId = race.RaceId,
                    ResultId = result.ResultId,
                    Year = race.Year,
                    Division = openDivision,
                    AgeCategory = null,
                    Points = openPoints,
                    IsRecordBonus = result.IsNewRecord,
                    CreatedAt = DateTime.UtcNow
                });

                pointsCreated++;
            }

            // Calculate Age Division points (top 5 per age category) — skip results with no
            // age info at all (neither an exact age nor a category was ever recorded)
            var ageCategory = GetResultAgeCategory(result);
            if (ageCategory != null)
            {
                var ageDivision = GrandPrixConstants.GetAgeDivision(gender);

                // Find place within age category
                var resultsInCategory = results
                    .Where(r => GetResultAgeCategory(r) == ageCategory)
                    .OrderBy(r => r.Time)
                    .ToList();

                var placeInCategory = resultsInCategory.IndexOf(result) + 1;
                result.PlaceAgeCategory = placeInCategory;

                if (placeInCategory <= 5)
                {
                    var agePoints = CalculateAgeDivisionPoints(placeInCategory);

                    _context.GrandPrixPoints.Add(new GrandPrixPoints
                    {
                        PointsId = Guid.NewGuid(),
                        RunnerId = result.RunnerId,
                        RaceId = race.RaceId,
                        ResultId = result.ResultId,
                        Year = race.Year,
                        Division = ageDivision,
                        AgeCategory = ageCategory,
                        Points = agePoints,
                        IsRecordBonus = false,
                        CreatedAt = DateTime.UtcNow
                    });

                    pointsCreated++;
                }
            }
        }

        return pointsCreated;
    }

    public async Task<int> UpdateStandingsAsync(int year)
    {
        _logger.LogInformation("Updating standings for year {Year}", year);

        await EnsureSeasonNotFinalizedAsync(year);

        // Delete existing standings for this year
        var existingStandings = await _context.GrandPrixStandings
            .Where(s => s.Year == year)
            .ToListAsync();

        _context.GrandPrixStandings.RemoveRange(existingStandings);

        var standingsCreated = 0;
        var gamutCompletions = await GetGamutCompletionsAsync(year);

        // Calculate standings for each division
        standingsCreated += await CalculateDivisionStandings(year, Division.OpenMale, null, gamutCompletions);
        standingsCreated += await CalculateDivisionStandings(year, Division.OpenFemale, null, gamutCompletions);
        standingsCreated += await CalculateDivisionStandings(year, Division.OpenNonbinary, null, gamutCompletions);

        // Calculate standings for each age category
        var ageCategories = GrandPrixConstants.AgeCategories.Select(c => c.Name);

        foreach (var category in ageCategories)
        {
            standingsCreated += await CalculateDivisionStandings(year, Division.AgeMale, category, gamutCompletions);
            standingsCreated += await CalculateDivisionStandings(year, Division.AgeFemale, category, gamutCompletions);
            standingsCreated += await CalculateDivisionStandings(year, Division.AgeNonbinary, category, gamutCompletions);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Created {Count} standing records for year {Year}", standingsCreated, year);

        return standingsCreated;
    }

    private async Task<int> CalculateDivisionStandings(
        int year, Division division, string? ageCategory, Dictionary<Guid, HashSet<Guid>> gamutCompletions)
    {
        // Get all points for this division
        var query = _context.GrandPrixPoints
            .Include(p => p.Runner)
            .Where(p => p.Year == year && p.Division == division);

        if (ageCategory != null)
        {
            query = query.Where(p => p.AgeCategory == ageCategory);
        }

        var allPoints = await query.ToListAsync();

        // Group by runner
        var runnerPoints = allPoints
            .GroupBy(p => p.RunnerId)
            .Select(g => new
            {
                RunnerId = g.Key,
                Runner = g.First().Runner,
                AllRacePoints = g.OrderByDescending(p => p.Points).ToList()
            })
            .ToList();

        var standings = new List<GrandPrixStanding>();

        foreach (var rp in runnerPoints)
        {
            var best4Points = rp.AllRacePoints.Take(BestRacesCount).ToList();
            var totalPoints = best4Points.Sum(p => p.Points);

            // Check eligibility: must have at least 1 race with points
            if (best4Points.Count < MinRacesForEligibility)
                continue;

            var standing = new GrandPrixStanding
            {
                StandingId = Guid.NewGuid(),
                RunnerId = rp.RunnerId,
                Year = year,
                Division = division,
                AgeCategory = ageCategory,
                TotalPoints = totalPoints,
                RacesCompleted = rp.AllRacePoints.Count,
                RacesCounted = best4Points.Count,
                BestRacePoints = best4Points.ElementAtOrDefault(0)?.Points ?? 0,
                SecondBestRacePoints = best4Points.ElementAtOrDefault(1)?.Points ?? 0,
                ThirdBestRacePoints = best4Points.ElementAtOrDefault(2)?.Points ?? 0,
                FourthBestRacePoints = best4Points.ElementAtOrDefault(3)?.Points ?? 0,
                RunTheGamutQualified = gamutCompletions.TryGetValue(rp.RunnerId, out var completed) &&
                                       completed.Count >= GrandPrixConstants.RunTheGamutMinRaces,
                LastUpdated = DateTime.UtcNow
            };

            standings.Add(standing);
        }

        // Sort standings by total points (desc), then by tiebreakers
        var sortedStandings = standings
            .OrderByDescending(s => s.TotalPoints)
            .ThenByDescending(s => s.BestRacePoints)
            .ThenByDescending(s => s.SecondBestRacePoints)
            .ThenByDescending(s => s.ThirdBestRacePoints)
            .ThenByDescending(s => s.FourthBestRacePoints)
            .ToList();

        // Assign ranks — tiebreakers already ordered the list; runners still level on total and
        // all four best races share a rank (standard competition ranking: 1, 2, 2, 4)
        for (int i = 0; i < sortedStandings.Count; i++)
        {
            sortedStandings[i].Rank = i > 0 && IsFullTie(sortedStandings[i], sortedStandings[i - 1])
                ? sortedStandings[i - 1].Rank
                : i + 1;
        }

        _context.GrandPrixStandings.AddRange(sortedStandings);

        return sortedStandings.Count;
    }

    private static bool IsFullTie(GrandPrixStanding a, GrandPrixStanding b) =>
        a.TotalPoints == b.TotalPoints &&
        a.BestRacePoints == b.BestRacePoints &&
        a.SecondBestRacePoints == b.SecondBestRacePoints &&
        a.ThirdBestRacePoints == b.ThirdBestRacePoints &&
        a.FourthBestRacePoints == b.FourthBestRacePoints;

    public async Task<List<GrandPrixStanding>> GetStandingsAsync(int year, Division division, string? ageCategory = null)
    {
        var query = _context.GrandPrixStandings
            .Include(s => s.Runner)
            .Where(s => s.Year == year && s.Division == division);

        if (ageCategory != null)
        {
            query = query.Where(s => s.AgeCategory == ageCategory);
        }

        return await query
            .OrderBy(s => s.Rank)
            .ToListAsync();
    }

    public async Task<List<GrandPrixPoints>> GetRunnerPointsAsync(Guid runnerId, int year)
    {
        return await _context.GrandPrixPoints
            .Include(p => p.Race)
            .Where(p => p.RunnerId == runnerId && p.Year == year)
            .OrderBy(p => p.Race!.Date)
            .ToListAsync();
    }

    public async Task<bool> RecalculateAfterResultsChangeAsync(Guid raceId)
    {
        try
        {
            var race = await _context.Races.FindAsync(raceId);
            if (race == null)
                return false;

            // A race that doesn't count and has no leftover points can't change standings
            if (!await AffectsStandingsAsync(race))
                return true;

            // Recalculate points for this race
            await CalculateRacePointsAsync(raceId);

            // Recalculate standings for the year
            await UpdateStandingsAsync(race.Year);

            return true;
        }
        catch (GrandPrixSeasonFinalizedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recalculating after results change for race {RaceId}", raceId);
            return false;
        }
    }

    public Task<bool> IsSeasonFinalizedAsync(int year) =>
        _context.GrandPrixSeasons.AnyAsync(s => s.Year == year && s.IsFinalized);

    /// <summary>Whether the race counts toward the Grand Prix or still has points from when it did.</summary>
    private async Task<bool> AffectsStandingsAsync(Race race) =>
        race.IsGrandPrixRace || await _context.GrandPrixPoints.AnyAsync(p => p.RaceId == race.RaceId);

    public async Task<Dictionary<Guid, HashSet<Guid>>> GetGamutCompletionsAsync(int year)
    {
        var gpSeriesIds = await _context.Races
            .Where(r => r.Year == year && r.IsGrandPrixRace)
            .Select(r => r.RaceVariant.RaceSeriesId)
            .Distinct()
            .ToListAsync();

        var finishes = await _context.RaceResults
            .Where(r => r.Status == ResultStatus.Finished &&
                        r.Race.Year == year &&
                        (r.Race.IsGrandPrixRace || r.Race.RaceVariant.CountsTowardGamut) &&
                        gpSeriesIds.Contains(r.Race.RaceVariant.RaceSeriesId))
            .Select(r => new { r.RunnerId, r.Race.RaceVariant.RaceSeriesId })
            .ToListAsync();

        return finishes
            .GroupBy(f => f.RunnerId)
            .ToDictionary(g => g.Key, g => g.Select(f => f.RaceSeriesId).ToHashSet());
    }

    private async Task EnsureSeasonNotFinalizedAsync(int year)
    {
        if (await IsSeasonFinalizedAsync(year))
            throw new GrandPrixSeasonFinalizedException(year);
    }
}
