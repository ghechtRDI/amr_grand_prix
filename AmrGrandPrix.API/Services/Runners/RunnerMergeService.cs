using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs;
using AmrGrandPrix.API.Services.GrandPrix;
using AmrGrandPrix.API.Services.ResultsProcessing;
using Microsoft.EntityFrameworkCore;

namespace AmrGrandPrix.API.Services.Runners;

/// <summary>
/// Duplicate detection and merging for <see cref="Runner"/> records — e.g. "Josh Taylor" and
/// "Joshua Taylor" uploaded from different races, whose points otherwise never add up.
/// </summary>
public class RunnerMergeService : IRunnerMergeService
{
    private const double MinimumNameSimilarity = 0.75;
    private const int MaxBirthYearDiscrepancy = 2;

    private readonly ApplicationDbContext _context;
    private readonly IRunnerMatchingService _matchingService;
    private readonly IGrandPrixCalculationService _grandPrixCalculationService;
    private readonly ILogger<RunnerMergeService> _logger;

    public RunnerMergeService(
        ApplicationDbContext context,
        IRunnerMatchingService matchingService,
        IGrandPrixCalculationService grandPrixCalculationService,
        ILogger<RunnerMergeService> logger)
    {
        _context = context;
        _matchingService = matchingService;
        _grandPrixCalculationService = grandPrixCalculationService;
        _logger = logger;
    }

    public async Task<List<PotentialDuplicateDto>> FindPotentialDuplicatesAsync(double minConfidence = 0.75, int limit = 200)
    {
        var runners = await _context.Runners.AsNoTracking().ToListAsync();
        var raceIdsByRunner = (await _context.RaceResults
                .Select(rr => new { rr.RunnerId, rr.RaceId })
                .ToListAsync())
            .GroupBy(r => r.RunnerId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.RaceId).ToHashSet());

        var candidates = runners
            .Select(r => (Runner: r, Names: NameCandidates(r), BirthYear: BirthYear(r)))
            .ToList();

        var pairs = new List<(Guid A, Guid B, double Confidence, double Name, bool Gender, bool? BirthYear)>();

        for (var i = 0; i < candidates.Count; i++)
        {
            for (var j = i + 1; j < candidates.Count; j++)
            {
                var a = candidates[i];
                var b = candidates[j];

                var nameSimilarity = BestNameSimilarity(a.Names, b.Names);
                if (nameSimilarity < MinimumNameSimilarity)
                    continue;

                var genderMatch = a.Runner.Gender == b.Runner.Gender;
                bool? birthYearMatch = a.BirthYear.HasValue && b.BirthYear.HasValue
                    ? Math.Abs(a.BirthYear.Value - b.BirthYear.Value) <= MaxBirthYearDiscrepancy
                    : null;

                // Name is the primary signal (70%); gender and birth year (15% each) corroborate
                // it. An unknown birth year doesn't count against the pair — it just can't help.
                var confidence = Math.Min(
                    nameSimilarity * 0.70 + (genderMatch ? 0.15 : 0.0) + (birthYearMatch != false ? 0.15 : 0.0),
                    1.0);
                if (confidence < minConfidence)
                    continue;

                // One person can't have two results in the same race.
                if (raceIdsByRunner.TryGetValue(a.Runner.RunnerId, out var aRaces) &&
                    raceIdsByRunner.TryGetValue(b.Runner.RunnerId, out var bRaces) &&
                    aRaces.Overlaps(bRaces))
                    continue;

                pairs.Add((a.Runner.RunnerId, b.Runner.RunnerId, confidence, nameSimilarity, genderMatch, birthYearMatch));
            }
        }

        var top = pairs
            .OrderByDescending(p => p.Confidence)
            .ThenByDescending(p => p.Name)
            .Take(limit)
            .ToList();

        var summaries = await GetAdminSummariesAsync(top.SelectMany(p => new[] { p.A, p.B }).Distinct().ToList());

        return top
            .Select(p =>
            {
                // Show the record with more results first — it's usually the one to keep.
                var (first, second) = summaries[p.A].ResultCount >= summaries[p.B].ResultCount ? (p.A, p.B) : (p.B, p.A);
                return new PotentialDuplicateDto
                {
                    RunnerA = summaries[first],
                    RunnerB = summaries[second],
                    Confidence = Math.Round(p.Confidence, 3),
                    NameSimilarity = Math.Round(p.Name, 3),
                    GenderMatch = p.Gender,
                    BirthYearMatch = p.BirthYear
                };
            })
            .ToList();
    }

    public async Task<Dictionary<Guid, RunnerAdminDto>> GetAdminSummariesAsync(IReadOnlyCollection<Guid> runnerIds)
    {
        var runners = await _context.Runners
            .AsNoTracking()
            .Where(r => runnerIds.Contains(r.RunnerId))
            .ToListAsync();

        var resultYears = (await _context.RaceResults
                .Where(rr => runnerIds.Contains(rr.RunnerId))
                .Select(rr => new { rr.RunnerId, rr.Race.Year })
                .ToListAsync())
            .GroupBy(r => r.RunnerId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Year).ToList());

        var linkedEmails = (await _context.Users
                .Where(u => u.RunnerId != null && runnerIds.Contains(u.RunnerId.Value))
                .Select(u => new { RunnerId = u.RunnerId!.Value, u.Email })
                .ToListAsync())
            .GroupBy(u => u.RunnerId)
            .ToDictionary(g => g.Key, g => g.First().Email);

        return runners.ToDictionary(r => r.RunnerId, r =>
        {
            var years = resultYears.GetValueOrDefault(r.RunnerId) ?? new List<int>();
            return new RunnerAdminDto
            {
                RunnerId = r.RunnerId,
                FirstName = r.FirstName,
                LastName = r.LastName,
                FullName = r.FullName,
                PreferredName = r.PreferredName,
                AlternateNames = r.AlternateNames.ToList(),
                Gender = r.Gender,
                DateOfBirth = r.DateOfBirth,
                EstimatedBirthYear = r.EstimatedBirthYear,
                Email = r.Email,
                ResultCount = years.Count,
                FirstYear = years.Count > 0 ? years.Min() : null,
                LastYear = years.Count > 0 ? years.Max() : null,
                LinkedAccountEmail = linkedEmails.GetValueOrDefault(r.RunnerId)
            };
        });
    }

    public async Task<RunnerMergePreviewDto?> PreviewMergeAsync(Guid targetId, Guid sourceId)
    {
        var plan = await BuildPlanAsync(targetId, sourceId);
        return plan?.Preview;
    }

    public async Task<RunnerMergeResultDto> MergeAsync(Guid targetId, Guid sourceId)
    {
        var plan = await BuildPlanAsync(targetId, sourceId)
                   ?? throw new InvalidOperationException("Runner not found");
        if (!plan.Preview.CanMerge)
            throw new InvalidOperationException(string.Join(" ", plan.Preview.BlockingIssues));

        var target = plan.Target;
        var source = plan.Source;
        var merged = plan.Preview.Merged;

        // Relational providers get a transaction so a failed standings recalculation rolls the
        // whole merge back; the in-memory test provider doesn't support transactions.
        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;

        foreach (var result in source.Results)
            result.RunnerId = target.RunnerId;

        var points = await _context.GrandPrixPoints.Where(p => p.RunnerId == source.RunnerId).ToListAsync();
        foreach (var p in points)
            p.RunnerId = target.RunnerId;

        // Standings are rebuilt from points below; drop the source's now-stale rows.
        _context.GrandPrixStandings.RemoveRange(
            await _context.GrandPrixStandings.Where(s => s.RunnerId == source.RunnerId).ToListAsync());

        var claims = await _context.RunnerClaims
            .Where(c => c.RunnerId == source.RunnerId || c.RunnerId == target.RunnerId)
            .ToListAsync();
        var targetClaimants = claims.Where(c => c.RunnerId == target.RunnerId).Select(c => c.ApplicationUserId).ToHashSet();
        foreach (var claim in claims.Where(c => c.RunnerId == source.RunnerId))
        {
            if (targetClaimants.Contains(claim.ApplicationUserId))
                _context.RunnerClaims.Remove(claim);
            else
                claim.RunnerId = target.RunnerId;
        }

        if (plan.SourceAccount != null)
            plan.SourceAccount.RunnerId = target.RunnerId;

        target.PreferredName = merged.PreferredName;
        target.AlternateNames = merged.AlternateNames;
        target.DateOfBirth = merged.DateOfBirth;
        target.EstimatedBirthYear = merged.EstimatedBirthYear;
        target.Email = merged.Email;
        target.UpdatedAt = DateTime.UtcNow;

        _context.Runners.Remove(source);
        await _context.SaveChangesAsync();

        foreach (var year in plan.Preview.GrandPrixYearsToRecalculate)
            await _grandPrixCalculationService.UpdateStandingsAsync(year);

        if (transaction != null)
            await transaction.CommitAsync();

        _logger.LogInformation(
            "Merged runner {SourceName} ({SourceId}) into {TargetName} ({TargetId}): {ResultCount} result(s) moved, standings recalculated for {Years}",
            source.FullName, source.RunnerId, target.FullName, target.RunnerId,
            plan.Preview.ResultsToMove, string.Join(", ", plan.Preview.GrandPrixYearsToRecalculate));

        var summary = await GetAdminSummariesAsync(new[] { target.RunnerId });
        return new RunnerMergeResultDto
        {
            Runner = summary[target.RunnerId],
            ResultsMoved = plan.Preview.ResultsToMove,
            YearsRecalculated = plan.Preview.GrandPrixYearsToRecalculate
        };
    }

    private sealed record MergePlan(Runner Target, Runner Source, ApplicationUser? SourceAccount, RunnerMergePreviewDto Preview);

    private async Task<MergePlan?> BuildPlanAsync(Guid targetId, Guid sourceId)
    {
        var runners = await _context.Runners
            .Include(r => r.Results)
                .ThenInclude(rr => rr.Race)
                .ThenInclude(r => r.RaceVariant).ThenInclude(v => v.RaceSeries).ThenInclude(s => s.Variants)
            .Where(r => r.RunnerId == targetId || r.RunnerId == sourceId)
            .ToListAsync();
        var target = runners.FirstOrDefault(r => r.RunnerId == targetId);
        var source = runners.FirstOrDefault(r => r.RunnerId == sourceId);
        if (target == null || source == null)
            return null;

        var summaries = await GetAdminSummariesAsync(new[] { targetId, sourceId });
        var preview = new RunnerMergePreviewDto
        {
            Target = summaries[targetId],
            Source = summaries[sourceId],
            ResultsToMove = source.Results.Count
        };

        if (targetId == sourceId)
        {
            preview.BlockingIssues.Add("A runner can't be merged into itself.");
            preview.Merged = BuildMerged(target, target);
            return new MergePlan(target, source, null, preview);
        }

        preview.Merged = BuildMerged(target, source);

        // Standings change in every year the source scored or placed; those years are rebuilt.
        var pointYears = await _context.GrandPrixPoints
            .Where(p => p.RunnerId == sourceId)
            .Select(p => p.Year)
            .Distinct()
            .ToListAsync();
        var standingYears = await _context.GrandPrixStandings
            .Where(s => s.RunnerId == sourceId)
            .Select(s => s.Year)
            .Distinct()
            .ToListAsync();
        preview.GrandPrixYearsToRecalculate = pointYears.Union(standingYears).OrderBy(y => y).ToList();

        var gpResultYears = source.Results.Where(r => r.Race.IsGrandPrixRace).Select(r => r.Race.Year);
        var affectedYears = preview.GrandPrixYearsToRecalculate.Union(gpResultYears).Distinct().ToList();
        preview.FinalizedYears = (await _context.GrandPrixSeasons
                .Where(s => s.IsFinalized && affectedYears.Contains(s.Year))
                .Select(s => s.Year)
                .ToListAsync())
            .OrderBy(y => y)
            .ToList();
        if (preview.FinalizedYears.Count > 0)
        {
            preview.BlockingIssues.Add(
                $"The {string.Join(", ", preview.FinalizedYears)} Grand Prix {(preview.FinalizedYears.Count == 1 ? "is" : "are")} " +
                "finalized and this merge would change its standings. Un-finalize it in Results Management first.");
        }

        var sharedRaces = source.Results
            .Where(sr => target.Results.Any(tr => tr.RaceId == sr.RaceId))
            .Select(sr => $"{RaceProjections.DisplayName(sr.Race)} {sr.Race.Year}")
            .Distinct()
            .ToList();
        if (sharedRaces.Count > 0)
        {
            preview.BlockingIssues.Add(
                $"Both runners have a result in {string.Join(", ", sharedRaces)}. One person can't finish a race twice, " +
                "so these are probably different people — if not, delete the duplicate result first.");
        }

        var accounts = await _context.Users
            .Where(u => u.RunnerId == targetId || u.RunnerId == sourceId)
            .ToListAsync();
        var targetAccount = accounts.FirstOrDefault(u => u.RunnerId == targetId);
        var sourceAccount = accounts.FirstOrDefault(u => u.RunnerId == sourceId);
        if (targetAccount != null && sourceAccount != null)
        {
            preview.BlockingIssues.Add(
                $"Both runners are linked to user accounts ({targetAccount.Email} and {sourceAccount.Email}). " +
                "Unlink one of them before merging.");
        }
        preview.MovesAccountLink = sourceAccount != null && targetAccount == null;

        if (target.Gender != source.Gender)
        {
            preview.Warnings.Add(
                $"Genders differ ({target.Gender} vs. {source.Gender}); the merged runner will be {target.Gender}. " +
                "Each result keeps the gender it was recorded with.");
        }

        if (target.DateOfBirth.HasValue && source.DateOfBirth.HasValue && target.DateOfBirth != source.DateOfBirth)
        {
            preview.Warnings.Add(
                $"Dates of birth differ ({target.DateOfBirth:yyyy-MM-dd} vs. {source.DateOfBirth:yyyy-MM-dd}); " +
                "the kept runner's date of birth is used.");
        }
        else if (BirthYear(target) is { } targetYear && BirthYear(source) is { } sourceYear &&
                 Math.Abs(targetYear - sourceYear) > MaxBirthYearDiscrepancy)
        {
            preview.Warnings.Add(
                $"Birth years look different (about {targetYear} vs. {sourceYear}) — double-check these are the same person.");
        }

        if (!string.IsNullOrWhiteSpace(target.Email) && !string.IsNullOrWhiteSpace(source.Email) &&
            !string.Equals(target.Email, source.Email, StringComparison.OrdinalIgnoreCase))
        {
            preview.Warnings.Add($"Emails differ; {target.Email} is kept and {source.Email} is discarded.");
        }

        return new MergePlan(target, source, preview.MovesAccountLink ? sourceAccount : null, preview);
    }

    /// <summary>
    /// The target keeps its legal name and gender; anything it's missing is filled from the
    /// source, and both runners' names become alternate names so future uploads under either
    /// name match the merged runner.
    /// </summary>
    private static MergedRunnerDto BuildMerged(Runner target, Runner source)
    {
        var preferredName = string.IsNullOrWhiteSpace(target.PreferredName) ? source.PreferredName : target.PreferredName;

        var alternateNames = RunnerNames.Normalize(
            target.AlternateNames
                .Append(target.FullName)
                .Append(source.FullName)
                .Append(string.Equals(source.PreferredName, preferredName, StringComparison.OrdinalIgnoreCase) ? null : source.PreferredName)
                .Concat(source.AlternateNames));

        return new MergedRunnerDto
        {
            FirstName = target.FirstName,
            LastName = target.LastName,
            PreferredName = preferredName,
            AlternateNames = alternateNames,
            Gender = target.Gender,
            DateOfBirth = target.DateOfBirth ?? source.DateOfBirth,
            EstimatedBirthYear = target.EstimatedBirthYear ?? source.EstimatedBirthYear,
            Email = string.IsNullOrWhiteSpace(target.Email) ? source.Email : target.Email
        };
    }

    private static int? BirthYear(Runner runner) => runner.DateOfBirth?.Year ?? runner.EstimatedBirthYear;

    /// <summary>
    /// Every full name a runner might appear under: legal, alternate, and preferred — the latter
    /// also paired with the last name when it's just a first name (e.g. "Josh" → "Josh Taylor").
    /// </summary>
    private static List<NameKey> NameCandidates(Runner runner)
    {
        var names = new List<string?> { runner.FullName };
        names.AddRange(runner.AlternateNames);
        if (!string.IsNullOrWhiteSpace(runner.PreferredName))
        {
            names.Add(runner.PreferredName);
            if (!runner.PreferredName.Trim().Contains(' '))
                names.Add($"{runner.PreferredName} {runner.LastName}");
        }

        return RunnerNames.Normalize(names).Select(n => new NameKey(n.ToLowerInvariant())).ToList();
    }

    /// <summary>A lowercased name plus its letter counts, for a cheap edit-distance lower bound.</summary>
    private sealed class NameKey
    {
        public string Text { get; }
        public int[] LetterCounts { get; } = new int[27];

        public NameKey(string text)
        {
            Text = text;
            foreach (var c in text)
                LetterCounts[c is >= 'a' and <= 'z' ? c - 'a' : 26]++;
        }
    }

    private double BestNameSimilarity(List<NameKey> namesA, List<NameKey> namesB)
    {
        var best = 0.0;
        foreach (var a in namesA)
        {
            foreach (var b in namesB)
            {
                // Comparing every pair of runners is O(n²), so skip the edit distance when a cheap
                // lower bound on it already rules the pair out: each edit changes the letter
                // counts by at most 2, and the distance is at least the length difference.
                var countDifference = 0;
                for (var k = 0; k < 27; k++)
                    countDifference += Math.Abs(a.LetterCounts[k] - b.LetterCounts[k]);
                var minDistance = Math.Max(Math.Abs(a.Text.Length - b.Text.Length), (countDifference + 1) / 2);
                var upperBound = 1.0 - minDistance / (double)Math.Max(a.Text.Length, b.Text.Length);
                if (upperBound < MinimumNameSimilarity || upperBound <= best)
                    continue;

                best = Math.Max(best, _matchingService.CalculateNameSimilarity(a.Text, b.Text));
                if (best >= 1.0)
                    return best;
            }
        }
        return best;
    }
}
