using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs.RaceResults;

namespace AmrGrandPrix.API.Services.ResultsProcessing;

/// <summary>
/// Service for matching race result rows to existing runners in the database
/// </summary>
public interface IRunnerMatchingService
{
    /// <summary>
    /// Find potential matches for a runner in the database
    /// </summary>
    /// <param name="name">Runner's full name</param>
    /// <param name="age">Runner's exact age (optional)</param>
    /// <param name="gender">Runner's gender (optional)</param>
    /// <param name="ageCategory">Runner's age category, used when an exact age isn't known (optional)</param>
    /// <param name="asOfDate">Date to compute each candidate runner's age as of (defaults to today when not a race-upload context)</param>
    /// <returns>List of potential matches with confidence scores</returns>
    Task<List<RunnerMatch>> FindMatchesAsync(string name, int? age = null, Gender? gender = null, string? ageCategory = null, DateOnly? asOfDate = null);

    /// <summary>
    /// Find matches for multiple result rows
    /// </summary>
    /// <param name="resultRows">Result rows to match</param>
    /// <param name="raceDate">Date of the race these rows belong to, used to compute each candidate runner's age at that race rather than today</param>
    /// <returns>Result rows with RunnerMatches populated</returns>
    Task<List<ResultRow>> FindMatchesForResultsAsync(List<ResultRow> resultRows, DateOnly raceDate);

    /// <summary>
    /// Calculate similarity between two names using Levenshtein distance
    /// </summary>
    /// <param name="name1">First name</param>
    /// <param name="name2">Second name</param>
    /// <returns>Similarity score (0.0 to 1.0)</returns>
    double CalculateNameSimilarity(string name1, string name2);
}

/// <summary>
/// Represents a potential match between a result row and an existing runner
/// </summary>
public class RunnerMatch
{
    public Guid RunnerId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public int? Age { get; set; }
    public bool HasVerifiedDateOfBirth { get; set; }
    public string? AgeCategory { get; set; }
    public Gender? Gender { get; set; }
    public double Confidence { get; set; }
    public bool NameMatch { get; set; }
    public bool AgeMatch { get; set; }
    public bool GenderMatch { get; set; }
}
