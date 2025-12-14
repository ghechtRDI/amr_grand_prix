using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs.RaceResults;
using Microsoft.EntityFrameworkCore;

namespace AmrGrandPrix.API.Services.ResultsProcessing;

/// <summary>
/// Service for matching race result rows to existing runners using fuzzy matching
/// </summary>
public class RunnerMatchingService : IRunnerMatchingService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RunnerMatchingService> _logger;

    // Matching thresholds
    private const double MinimumNameSimilarity = 0.75; // 75% similarity required
    private const int MaxAgeDiscrepancy = 2; // Allow 2 years difference

    public RunnerMatchingService(
        ApplicationDbContext context,
        ILogger<RunnerMatchingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<RunnerMatch>> FindMatchesAsync(string name, int? age = null, Gender? gender = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new List<RunnerMatch>();
        }

        _logger.LogDebug("Finding matches for: {Name}, Age: {Age}, Gender: {Gender}", name, age, gender);

        // Get all runners from database
        var allRunners = await _context.Runners.ToListAsync();

        var matches = new List<RunnerMatch>();

        foreach (var runner in allRunners)
        {
            var fullName = $"{runner.FirstName} {runner.LastName}";
            var similarity = CalculateNameSimilarity(name, fullName);

            // Only consider matches above minimum similarity threshold
            if (similarity < MinimumNameSimilarity)
                continue;

            // Check age match
            var ageMatch = true;
            int? runnerAge = null;

            if (runner.DateOfBirth.HasValue)
            {
                runnerAge = CalculateAge(runner.DateOfBirth.Value);

                if (age.HasValue)
                {
                    var ageDifference = Math.Abs(age.Value - runnerAge.Value);
                    ageMatch = ageDifference <= MaxAgeDiscrepancy;
                }
            }
            else if (age.HasValue)
            {
                // No DOB in database, can't verify age
                ageMatch = false;
            }

            // Check gender match
            var genderMatch = !gender.HasValue || runner.Gender == gender.Value;

            // Calculate overall confidence
            var confidence = CalculateConfidence(similarity, ageMatch, genderMatch);

            matches.Add(new RunnerMatch
            {
                RunnerId = runner.RunnerId,
                FirstName = runner.FirstName,
                LastName = runner.LastName,
                Age = runnerAge,
                Gender = runner.Gender,
                Confidence = confidence,
                NameMatch = similarity >= 0.90,
                AgeMatch = ageMatch,
                GenderMatch = genderMatch
            });
        }

        // Sort by confidence (highest first)
        var sortedMatches = matches
            .OrderByDescending(m => m.Confidence)
            .ThenByDescending(m => m.NameMatch)
            .ThenByDescending(m => m.AgeMatch)
            .ToList();

        _logger.LogDebug("Found {Count} potential matches for '{Name}'", sortedMatches.Count, name);

        return sortedMatches;
    }

    public async Task<List<ResultRow>> FindMatchesForResultsAsync(List<ResultRow> resultRows)
    {
        _logger.LogInformation("Finding runner matches for {Count} result rows", resultRows.Count);

        foreach (var row in resultRows)
        {
            if (string.IsNullOrWhiteSpace(row.Name))
                continue;

            var matches = await FindMatchesAsync(row.Name, row.Age, row.Gender);

            // Only include matches with reasonable confidence (>= 0.70)
            row.RunnerMatches = matches
                .Where(m => m.Confidence >= 0.70)
                .Select(RunnerMatchDto.FromRunnerMatch)
                .ToList();

            // Auto-select if there's a very high confidence match (>= 0.95)
            var topMatch = row.RunnerMatches.FirstOrDefault();
            if (topMatch != null && topMatch.Confidence >= 0.95)
            {
                row.MatchedRunnerId = topMatch.RunnerId;
                _logger.LogDebug("Auto-matched '{Name}' to runner {RunnerId} (confidence: {Confidence:P0})",
                    row.Name, topMatch.RunnerId, topMatch.Confidence);
            }

            // Add info if this is a new runner
            if (row.RunnerMatches?.Count == 0)
            {
                row.ValidationIssues.Add(new ValidationIssue
                {
                    Field = "Runner",
                    Severity = ValidationSeverity.Info,
                    Message = "New runner - not found in database"
                });
            }
        }

        return resultRows;
    }

    public double CalculateNameSimilarity(string name1, string name2)
    {
        if (string.IsNullOrWhiteSpace(name1) || string.IsNullOrWhiteSpace(name2))
            return 0.0;

        // Normalize: lowercase, remove extra whitespace
        var normalized1 = NormalizeName(name1);
        var normalized2 = NormalizeName(name2);

        // Exact match
        if (normalized1 == normalized2)
            return 1.0;

        // Calculate Levenshtein distance
        var distance = LevenshteinDistance(normalized1, normalized2);
        var maxLength = Math.Max(normalized1.Length, normalized2.Length);

        // Convert distance to similarity (0.0 to 1.0)
        var similarity = 1.0 - (distance / (double)maxLength);

        return similarity;
    }

    private string NormalizeName(string name)
    {
        // Lowercase, trim, and normalize whitespace
        return string.Join(" ", name.ToLowerInvariant()
            .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
    }

    private int LevenshteinDistance(string s1, string s2)
    {
        var len1 = s1.Length;
        var len2 = s2.Length;

        // Create a 2D array to store distances
        var d = new int[len1 + 1, len2 + 1];

        // Initialize first column and row
        for (var i = 0; i <= len1; i++)
            d[i, 0] = i;

        for (var j = 0; j <= len2; j++)
            d[0, j] = j;

        // Calculate distances
        for (var i = 1; i <= len1; i++)
        {
            for (var j = 1; j <= len2; j++)
            {
                var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;

                d[i, j] = Math.Min(
                    Math.Min(
                        d[i - 1, j] + 1,      // deletion
                        d[i, j - 1] + 1),     // insertion
                    d[i - 1, j - 1] + cost);  // substitution
            }
        }

        return d[len1, len2];
    }

    private double CalculateConfidence(double nameSimilarity, bool ageMatch, bool genderMatch)
    {
        // Name similarity is weighted most heavily (70%)
        var confidence = nameSimilarity * 0.70;

        // Age match adds 15%
        if (ageMatch)
            confidence += 0.15;

        // Gender match adds 15%
        if (genderMatch)
            confidence += 0.15;

        return Math.Min(confidence, 1.0);
    }

    private int CalculateAge(DateTime dateOfBirth)
    {
        var today = DateTime.Today;
        var age = today.Year - dateOfBirth.Year;

        // Subtract a year if birthday hasn't occurred yet this year
        if (dateOfBirth.Date > today.AddYears(-age))
            age--;

        return age;
    }
}
