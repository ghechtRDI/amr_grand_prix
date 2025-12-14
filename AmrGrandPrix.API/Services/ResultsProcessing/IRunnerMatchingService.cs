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
    /// <param name="age">Runner's age (optional)</param>
    /// <param name="gender">Runner's gender (optional)</param>
    /// <returns>List of potential matches with confidence scores</returns>
    Task<List<RunnerMatch>> FindMatchesAsync(string name, int? age = null, Gender? gender = null);

    /// <summary>
    /// Find matches for multiple result rows
    /// </summary>
    /// <param name="resultRows">Result rows to match</param>
    /// <returns>Result rows with RunnerMatches populated</returns>
    Task<List<ResultRow>> FindMatchesForResultsAsync(List<ResultRow> resultRows);

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
    public Gender? Gender { get; set; }
    public double Confidence { get; set; }
    public bool NameMatch { get; set; }
    public bool AgeMatch { get; set; }
    public bool GenderMatch { get; set; }
}
