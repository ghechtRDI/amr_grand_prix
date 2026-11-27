using AmrGrandPrix.API.Common;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs.RaceResults;
using AmrGrandPrix.API.Services.Runners;
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

    // How far (in years) a row's age may sit outside the runner's birth-date range and still
    // agree: absorbs results that report age as of Dec 31 or another reference date.
    public const int AgeToleranceYears = 1;

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

        var candidates = await LoadCandidatesAsync();
        return ScoreMatches(candidates, name, age, gender, ageCategory, asOfDate ?? DateOnly.FromDateTime(DateTime.Today));
    }

    /// <summary>A runner plus its name candidates, computed once per batch rather than once per row.</summary>
    private sealed record Candidate(Runner Runner, List<string> Names);

    private async Task<List<Candidate>> LoadCandidatesAsync() =>
        (await _context.Runners.ToListAsync())
            .Select(r => new Candidate(r, RunnerNames.NameCandidates(r)))
            .ToList();

    /// <summary>
    /// Scores every runner in <paramref name="candidates"/> against the given name/age/gender.
    /// Takes the candidate list so batch callers can load it once instead of once per row.
    /// The rules are documented in DOCS/RUNNER_MATCHING.md.
    /// </summary>
    private List<RunnerMatch> ScoreMatches(IReadOnlyList<Candidate> candidates, string name, int? age, Gender? gender, string? ageCategory, DateOnly effectiveAsOfDate)
    {
        _logger.LogDebug("Finding matches for: {Name}, Age: {Age}, AgeCategory: {AgeCategory}, Gender: {Gender}, AsOf: {AsOf}",
            name, age, ageCategory, gender, effectiveAsOfDate);

        var matches = new List<RunnerMatch>();
        var rowRange = BirthDateRange.FromResult(age, ageCategory, effectiveAsOfDate);

        foreach (var (runner, names) in candidates)
        {
            var similarity = CalculateBestNameSimilarity(name, names);

            // Only consider matches above minimum similarity threshold
            if (similarity < MinimumNameSimilarity)
                continue;

            // Age check: the birth dates the row's age (or category) allows must overlap the
            // runner's known range, give or take AgeToleranceYears. true = agrees, false =
            // disagrees, null = can't tell (either side unknown). Unknown is neutral — it doesn't
            // count against the runner, it just can't help.
            var runnerRange = AgeCalculator.GetBirthRange(runner);
            bool? ageMatch = runnerRange.IsUnknown || rowRange.IsUnknown
                ? null
                : runnerRange.Widen(AgeToleranceYears).Overlaps(rowRange);

            var runnerAge = AgeCalculator.GetRunnerAge(runner, effectiveAsOfDate);
            var (runnerAgeMin, runnerAgeMax) = AgeCalculator.GetAgeRange(runnerRange, effectiveAsOfDate);

            // Check gender match
            var genderMatch = !gender.HasValue || runner.Gender == gender.Value;

            // Calculate overall confidence
            var confidence = CalculateConfidence(similarity, ageMatch != false, genderMatch);

            matches.Add(new RunnerMatch
            {
                RunnerId = runner.RunnerId,
                FirstName = runner.FirstName,
                LastName = runner.LastName,
                KnownNames = names,
                Age = runnerAge,
                AgeMin = runnerAgeMin,
                AgeMax = runnerAgeMax,
                HasVerifiedDateOfBirth = runner.DateOfBirth.HasValue,
                AgeCategory = runnerAge.HasValue ? GrandPrixConstants.GetAgeCategory(runnerAge.Value) : null,
                Gender = runner.Gender,
                Confidence = confidence,
                NameMatch = similarity >= 0.90,
                // Reported only when confirmed, so a candidate whose age actually agrees sorts
                // above an equally scored one with no age on file.
                AgeMatch = ageMatch == true,
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

        // Load the runner table once for the whole batch rather than once per row
        var candidates = await LoadCandidatesAsync();

        foreach (var row in resultRows)
        {
            if (string.IsNullOrWhiteSpace(row.Name))
                continue;

            var matches = ScoreMatches(candidates, row.Name, row.Age, row.Gender, row.AgeCategory, raceDate);

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

            // No candidates isn't a validation issue: the row is simply saved as a new runner,
            // which Data Review already shows via its "New runner" match status.
        }

        return resultRows;
    }

    /// <summary>
    /// Best similarity between <paramref name="candidateName"/> and any of a runner's
    /// <see cref="RunnerNames.NameCandidates(Runner)"/> (legal name first, so an exact legal-name
    /// match returns immediately).
    /// </summary>
    private double CalculateBestNameSimilarity(string candidateName, IEnumerable<string> runnerNames)
    {
        var best = 0.0;
        foreach (var runnerName in runnerNames)
        {
            best = Math.Max(best, CalculateNameSimilarity(candidateName, runnerName));
            if (best >= 1.0)
                break;
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
            // rarely set. The estimated birth date range (narrowed from reported ages) is the
            // realistic signal here: the user's DOB falling inside a narrow range (one reported
            // age or tighter) counts the same as an exact verified DOB match. Category-only
            // ranges span a decade and say too little to count.
            var dobMatch = user.DateOfBirth.HasValue && runner.DateOfBirth.HasValue &&
                           user.DateOfBirth.Value == runner.DateOfBirth.Value;

            var estimatedRange = runner.EstimatedBirthRange;
            var rangeMatch = !runner.DateOfBirth.HasValue && user.DateOfBirth.HasValue &&
                             estimatedRange.IsNarrow && estimatedRange.Contains(user.DateOfBirth.Value);

            var yearOfBirthMatch = dobMatch || rangeMatch;

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
                // Name (60%) is the primary signal; a birth-date match — verified DOB or, failing
                // that, the estimated range — (30%) and a gender match (10%) corroborate it.
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
    /// Best similarity between any of the user's names (legal, alternate, preferred — paired with
    /// their last name when it's a single word) and any of the runner's name candidates.
    /// </summary>
    private double CalculateBestProfileNameSimilarity(ApplicationUser user, Runner runner)
    {
        var userNames = RunnerNames.NameCandidates(
            $"{user.FirstName} {user.LastName}", user.LastName, user.PreferredName, user.AlternateNames);
        var runnerNames = RunnerNames.NameCandidates(runner);

        var best = 0.0;
        foreach (var userName in userNames)
        {
            best = Math.Max(best, CalculateBestNameSimilarity(userName, runnerNames));
            if (best >= 1.0)
                break;
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

    private static readonly char[] NameSeparators = [' ', '\t'];

    private static string NormalizeName(string name)
    {
        // Lowercase, trim, and normalize whitespace
        return string.Join(" ", name.ToLowerInvariant()
            .Split(NameSeparators, StringSplitOptions.RemoveEmptyEntries));
    }

    private static int LevenshteinDistance(string s1, string s2)
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

    private static double CalculateConfidence(double nameSimilarity, bool ageMatch, bool genderMatch)
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
