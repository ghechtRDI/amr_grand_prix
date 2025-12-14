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

    // Open Division scoring table
    private static readonly Dictionary<int, int> OpenDivisionPoints = new()
    {
        { 1, 100 },
        { 2, 90 },
        { 3, 85 },
        { 4, 80 },
        { 5, 75 },
        { 6, 70 },
        { 7, 65 },
        { 8, 60 },
        { 9, 55 },
        { 10, 50 },
        { 11, 45 },
        { 12, 40 },
        { 13, 35 },
        { 14, 30 },
        { 15, 25 },
        { 16, 20 },
        { 17, 15 },
        { 18, 10 },
        { 19, 5 },
        { 20, 1 }
    };

    private const int RecordBonusPoints = 10;
    private const int MinRacesForEligibility = 1; // Must finish top 20/5 in at least 1 race
    private const int BestRacesCount = 4; // Count best 4 races
    private const int RunTheGamutMinRaces = 7; // Need 7 of 9 races for "Run the Gamut"

    public GrandPrixCalculationService(
        ApplicationDbContext context,
        ILogger<GrandPrixCalculationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public int CalculateOpenDivisionPoints(int placeInGender, bool isNewRecord = false)
    {
        var basePoints = OpenDivisionPoints.GetValueOrDefault(placeInGender, 0);
        var bonus = isNewRecord ? RecordBonusPoints : 0;
        return basePoints + bonus;
    }

    public int CalculateAgeDivisionPoints(int placeInAgeCategory)
    {
        return placeInAgeCategory switch
        {
            1 => 5,
            2 => 4,
            3 => 3,
            4 => 2,
            5 => 1,
            _ => 0
        };
    }

    public string DetermineAgeCategory(int age)
    {
        if (age <= 17) return "17 and Under";
        if (age <= 29) return "19-29";
        if (age <= 39) return "30-39";
        if (age <= 49) return "40-49";
        if (age <= 59) return "50-59";
        if (age <= 69) return "60-69";
        if (age <= 79) return "70-79";
        return "80-89";
    }

    public async Task<int> CalculateRacePointsAsync(Guid raceId)
    {
        _logger.LogInformation("Calculating points for race {RaceId}", raceId);

        var race = await _context.Races
            .Include(r => r.Results)
            .ThenInclude(r => r.Runner)
            .FirstOrDefaultAsync(r => r.RaceId == raceId);

        if (race == null)
        {
            _logger.LogWarning("Race {RaceId} not found", raceId);
            return 0;
        }

        if (!race.IsGrandPrixRace)
        {
            _logger.LogInformation("Race {RaceId} is not a Grand Prix race, skipping points calculation", raceId);
            return 0;
        }

        // Delete existing points for this race
        var existingPoints = await _context.GrandPrixPoints
            .Where(p => p.RaceId == raceId)
            .ToListAsync();

        _context.GrandPrixPoints.RemoveRange(existingPoints);
        await _context.SaveChangesAsync();

        // Get finished results only
        var results = race.Results
            .Where(r => r.Status == ResultStatus.Finished && r.Time.HasValue)
            .OrderBy(r => r.Time)
            .ToList();

        var pointsCreated = 0;

        // Calculate place within gender
        var maleResults = results.Where(r => r.Gender == Gender.Male).ToList();
        var femaleResults = results.Where(r => r.Gender == Gender.Female).ToList();

        pointsCreated += await CalculateGenderDivisionPoints(race, maleResults, Gender.Male);
        pointsCreated += await CalculateGenderDivisionPoints(race, femaleResults, Gender.Female);

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

            // Update PlaceGender on the result
            result.PlaceGender = placeInGender;

            // Calculate Open Division points (top 20)
            if (placeInGender <= 20)
            {
                var openPoints = CalculateOpenDivisionPoints(placeInGender, result.IsNewRecord);
                var openDivision = gender == Gender.Male ? Division.OpenMale : Division.OpenFemale;

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

            // Calculate Age Division points (top 5 per age category)
            var ageCategory = DetermineAgeCategory(result.Age);
            var ageDivision = gender == Gender.Male ? Division.AgeMale : Division.AgeFemale;

            // Find place within age category
            var resultsInCategory = results
                .Where(r => DetermineAgeCategory(r.Age) == ageCategory)
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

        return pointsCreated;
    }

    public async Task<int> UpdateStandingsAsync(int year)
    {
        _logger.LogInformation("Updating standings for year {Year}", year);

        // Delete existing standings for this year
        var existingStandings = await _context.GrandPrixStandings
            .Where(s => s.Year == year)
            .ToListAsync();

        _context.GrandPrixStandings.RemoveRange(existingStandings);

        var standingsCreated = 0;

        // Calculate standings for each division
        standingsCreated += await CalculateDivisionStandings(year, Division.OpenMale, null);
        standingsCreated += await CalculateDivisionStandings(year, Division.OpenFemale, null);

        // Calculate standings for each age category
        var ageCategories = new[] { "17 and Under", "19-29", "30-39", "40-49", "50-59", "60-69", "70-79", "80-89" };

        foreach (var category in ageCategories)
        {
            standingsCreated += await CalculateDivisionStandings(year, Division.AgeMale, category);
            standingsCreated += await CalculateDivisionStandings(year, Division.AgeFemale, category);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Created {Count} standing records for year {Year}", standingsCreated, year);

        return standingsCreated;
    }

    private async Task<int> CalculateDivisionStandings(int year, Division division, string? ageCategory)
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
                RunTheGamutQualified = rp.AllRacePoints.Count >= RunTheGamutMinRaces,
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

        // Assign ranks
        for (int i = 0; i < sortedStandings.Count; i++)
        {
            sortedStandings[i].Rank = i + 1;
        }

        _context.GrandPrixStandings.AddRange(sortedStandings);

        return sortedStandings.Count;
    }

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

            // Recalculate points for this race
            await CalculateRacePointsAsync(raceId);

            // Recalculate standings for the year
            await UpdateStandingsAsync(race.Year);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recalculating after results change for race {RaceId}", raceId);
            return false;
        }
    }
}