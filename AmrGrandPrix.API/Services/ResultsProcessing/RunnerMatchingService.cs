using AmrGrandPrix.API.Common;
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

    public async Task<List<RunnerMatch>> FindMatchesAsync(string name, int? age = null, Gender? gender = null, string? ageCategory = null, DateOnly? asOfDate = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new List<RunnerMatch>();
        }

        var effectiveAsOfDate = asOfDate ?? DateOnly.FromDateTime(DateTime.Today);

        _logger.LogDebug("Finding matches for: {Name}, Age: {Age}, AgeCategory: {AgeCategory}, Gender: {Gender}, AsOf: {AsOf}",
            name, age, ageCategory, gender, effectiveAsOfDate);

        // Get all runners from database
        var allRunners = await _context.Runners.ToListAsync();

        var matches = new List<RunnerMatch>();

        foreach (var runner in allRunners)
        {
            var similarity = CalculateBestNameSimilarity(name, runner);

            // Only consider matches above minimum similarity threshold
            if (similarity < MinimumNameSimilarity)
                continue;

            // Check age match
            var ageMatch = true;
            var runnerAge = AgeCalculator.GetRunnerAge(runner, effectiveAsOfDate);
            string? runnerAgeCategory = null;

            if (runnerAge.HasValue)
            {
                runnerAgeCategory = GrandPrixConstants.GetAgeCategory(runnerAge.Value);

                if (age.HasValue)
                {
                    var ageDifference = Math.Abs(age.Value - runnerAge.Value);
                    ageMatch = ageDifference <= MaxAgeDiscrepancy;
                }
                else if (!string.IsNullOrEmpty(ageCategory))
                {
                    // Only a category is known for the incoming row — compare at the category
                    // level instead of demanding an exact-age match.
                    ageMatch = string.Equals(runnerAgeCategory, ageCategory, StringComparison.OrdinalIgnoreCase);
                }
            }
            else if (age.HasValue || !string.IsNullOrEmpty(ageCategory))
            {
                // No DOB or estimated birth year in database, can't verify age
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
                HasVerifiedDateOfBirth = runner.DateOfBirth.HasValue,
                AgeCategory = runnerAgeCategory,
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

    public async Task<List<ResultRow>> FindMatchesForResultsAsync(List<ResultRow> resultRows, DateOnly raceDate)
    {
        _logger.LogInformation("Finding runner matches for {Count} result rows", resultRows.Count);

        foreach (var row in resultRows)
        {
            if (string.IsNullOrWhiteSpace(row.Name))
                continue;

            var matches = await FindMatchesAsync(row.Name, row.Age, row.Gender, row.AgeCategory, raceDate);

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

    /// <summary>
    /// Compares <paramref name="candidateName"/> against the runner's legal full name first.
    /// Only if that isn't a 100% match does it also try the runner's preferred name and each
    /// alternate name, returning the best score found across all of them.
    /// </summary>
    private double CalculateBestNameSimilarity(string candidateName, Runner runner)
    {
        var legalNameSimilarity = CalculateNameSimilarity(candidateName, runner.FullName);
        if (legalNameSimilarity >= 1.0)
            return legalNameSimilarity;

        var best = legalNameSimilarity;

        if (!string.IsNullOrWhiteSpace(runner.PreferredName))
            best = Math.Max(best, CalculateNameSimilarity(candidateName, runner.PreferredName));

        foreach (var alternateName in runner.AlternateNames)
        {
            if (string.IsNullOrWhiteSpace(alternateName))
                continue;
            best = Math.Max(best, CalculateNameSimilarity(candidateName, alternateName));
        }

        return best;
    }

    public async Task<List<RunnerMatch>> FindMatchesForProfileAsync(ApplicationUser user)
    {
        if (string.IsNullOrWhiteSpace(user.FirstName) || string.IsNullOrWhiteSpace(user.LastName))
            return new List<RunnerMatch>();

        var claimedRunnerIds = await _context.Users
            .Where(u => u.RunnerId != null)
            .Select(u => u.RunnerId!.Value)
            .ToListAsync();

        var excludedRunnerIds = await _context.RunnerClaims
            .Where(c => c.ApplicationUserId == user.Id &&
                        (c.Status == ClaimStatus.Pending || c.Status == ClaimStatus.Approved))
            .Select(c => c.RunnerId)
            .ToListAsync();

        var candidateRunners = await _context.Runners
            .Where(r => !claimedRunnerIds.Contains(r.RunnerId) && !excludedRunnerIds.Contains(r.RunnerId))
            .ToListAsync();

        var matches = new List<RunnerMatch>();

        foreach (var runner in candidateRunners)
        {
            var similarity = CalculateBestProfileNameSimilarity(user, runner);
            if (similarity < MinimumNameSimilarity)
                continue;

            // Race results essentially never carry a verified DOB, so a runner's DateOfBirth is
            // rarely set. EstimatedBirthYear (derived from a reported age) is the realistic
            // signal here, so a birth-year match counts the same as an exact verified DOB match.
            var dobMatch = user.DateOfBirth.HasValue && runner.DateOfBirth.HasValue &&
                           user.DateOfBirth.Value == runner.DateOfBirth.Value;

            var birthYearMatch = !dobMatch && user.DateOfBirth.HasValue && runner.EstimatedBirthYear.HasValue &&
                                  user.DateOfBirth.Value.Year == runner.EstimatedBirthYear.Value;

            var yearOfBirthMatch = dobMatch || birthYearMatch;

            // The user isn't required to set a gender on their profile, so an unset gender
            // doesn't penalize the match — it just means this signal has nothing to add.
            var genderMatch = !user.Gender.HasValue || runner.Gender == user.Gender.Value;

            var runnerAge = AgeCalculator.GetRunnerAge(runner, DateOnly.FromDateTime(DateTime.Today));
            var runnerAgeCategory = runnerAge.HasValue ? GrandPrixConstants.GetAgeCategory(runnerAge.Value) : null;

            matches.Add(new RunnerMatch
            {
                RunnerId = runner.RunnerId,
                FirstName = runner.FirstName,
                LastName = runner.LastName,
                Age = runnerAge,
                HasVerifiedDateOfBirth = runner.DateOfBirth.HasValue,
                AgeCategory = runnerAgeCategory,
                Gender = runner.Gender,
                // Name (60%) is the primary signal; a year-of-birth match — verified DOB or,
                // failing that, estimated birth year — (30%) and a gender match (10%) corroborate it.
                Confidence = Math.Min((similarity * 0.60) + (yearOfBirthMatch ? 0.30 : 0.0) + (genderMatch ? 0.10 : 0.0), 1.0),
                NameMatch = similarity >= 0.90,
                AgeMatch = yearOfBirthMatch,
                GenderMatch = genderMatch
            });
        }

        return matches
            .OrderByDescending(m => m.Confidence)
            .ThenByDescending(m => m.NameMatch)
            .ToList();
    }

    /// <summary>
    /// Compares the user's legal name against the runner first; only if that isn't a 100% match
    /// does it also try the user's preferred name and each alternate name (each itself checked
    /// against the runner's legal, preferred, and alternate names via <see cref="CalculateBestNameSimilarity"/>).
    /// </summary>
    private double CalculateBestProfileNameSimilarity(ApplicationUser user, Runner runner)
    {
        var legalName = $"{user.FirstName} {user.LastName}".Trim();
        var best = CalculateBestNameSimilarity(legalName, runner);
        if (best >= 1.0)
            return best;

        if (!string.IsNullOrWhiteSpace(user.PreferredName))
            best = Math.Max(best, CalculateBestNameSimilarity(user.PreferredName, runner));

        foreach (var alternateName in user.AlternateNames)
        {
            if (string.IsNullOrWhiteSpace(alternateName))
                continue;
            best = Math.Max(best, CalculateBestNameSimilarity(alternateName, runner));
        }

        return best;
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

}
