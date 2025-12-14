using AmrGrandPrix.API.Models;

namespace AmrGrandPrix.API.Services.GrandPrix;

/// <summary>
/// Service for calculating Grand Prix points and standings
/// </summary>
public interface IGrandPrixCalculationService
{
    /// <summary>
    /// Calculate points for all results in a race
    /// </summary>
    /// <param name="raceId">Race to calculate points for</param>
    /// <returns>Number of point records created</returns>
    Task<int> CalculateRacePointsAsync(Guid raceId);

    /// <summary>
    /// Calculate Open Division points for a specific result
    /// </summary>
    /// <param name="placeInGender">Place within gender (1-20 for points)</param>
    /// <param name="isNewRecord">Whether this result set a new record</param>
    /// <returns>Points awarded</returns>
    int CalculateOpenDivisionPoints(int placeInGender, bool isNewRecord = false);

    /// <summary>
    /// Calculate Age Division points for a specific result
    /// </summary>
    /// <param name="placeInAgeCategory">Place within age category (1-5 for points)</param>
    /// <returns>Points awarded</returns>
    int CalculateAgeDivisionPoints(int placeInAgeCategory);

    /// <summary>
    /// Determine age category from age
    /// </summary>
    /// <param name="age">Runner's age</param>
    /// <returns>Age category string (e.g., "30-39")</returns>
    string DetermineAgeCategory(int age);

    /// <summary>
    /// Update overall standings for a given year
    /// </summary>
    /// <param name="year">Year to calculate standings for</param>
    /// <returns>Number of standing records updated</returns>
    Task<int> UpdateStandingsAsync(int year);

    /// <summary>
    /// Get standings for a specific division and year
    /// </summary>
    /// <param name="year">Year</param>
    /// <param name="division">Division (OpenMale, OpenFemale, AgeMale, AgeFemale)</param>
    /// <param name="ageCategory">Age category (required for Age divisions)</param>
    /// <returns>Ordered list of standings</returns>
    Task<List<GrandPrixStanding>> GetStandingsAsync(int year, Division division, string? ageCategory = null);

    /// <summary>
    /// Get a runner's points history for a specific year
    /// </summary>
    /// <param name="runnerId">Runner ID</param>
    /// <param name="year">Year</param>
    /// <returns>List of points records</returns>
    Task<List<GrandPrixPoints>> GetRunnerPointsAsync(Guid runnerId, int year);

    /// <summary>
    /// Recalculate all standings after results are modified
    /// </summary>
    /// <param name="raceId">Race that was modified</param>
    /// <returns>True if successful</returns>
    Task<bool> RecalculateAfterResultsChangeAsync(Guid raceId);
}
