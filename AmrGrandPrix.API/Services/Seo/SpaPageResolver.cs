using Microsoft.EntityFrameworkCore;
using AmrGrandPrix.API.Data;

namespace AmrGrandPrix.API.Services.Seo;

/// <summary>How the server should answer a request for a client-side (SPA) route.</summary>
public sealed record SpaPage(int StatusCode, bool NoIndex)
{
    public static readonly SpaPage Indexable = new(StatusCodes.Status200OK, false);
    public static readonly SpaPage NoIndexPage = new(StatusCodes.Status200OK, true);
    public static readonly SpaPage NotFound = new(StatusCodes.Status404NotFound, true);
}

/// <summary>
/// Mirrors the client's route table (<c>AmrGrandPrix.Client/src/App.jsx</c>) so the app shell is
/// served with a real status: 404 for unknown paths and for IDs that don't exist (instead of a
/// "soft 404" 200), and noindex for private pages (auth, profile, admin) and for runners under 18.
/// Matching is case-insensitive and ignores a trailing slash, like React Router.
/// </summary>
public class SpaPageResolver
{
    private static readonly HashSet<string> IndexableRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "", "standings", "race-results", "athletes", "report"
    };

    private static readonly HashSet<string> NoIndexRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "login", "register", "confirm-email", "forgot-password", "reset-password", "unauthorized",
        "profile", "admin/results", "admin/results/upload", "admin/runners", "admin/users"
    };

    private readonly ApplicationDbContext _context;

    public SpaPageResolver(ApplicationDbContext context) => _context = context;

    public async Task<SpaPage> ResolveAsync(string path, CancellationToken ct = default)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var joined = string.Join('/', segments);

        if (IndexableRoutes.Contains(joined))
            return SpaPage.Indexable;
        if (NoIndexRoutes.Contains(joined))
            return SpaPage.NoIndexPage;

        return segments switch
        {
            [var s, var year] when Is(s, "standings") => await StandingsYearAsync(year, ct),
            [var s, var id] when Is(s, "race-series") => await SeriesAsync(id, ct),
            [var s, var id, var r] when Is(s, "races") && Is(r, "results") => await RaceAsync(id, ct),
            [var s, var id] when Is(s, "runners") => await RunnerAsync(id, null, ct),
            [var s, var id, var se, var seriesId] when Is(s, "runners") && Is(se, "series") =>
                await RunnerAsync(id, seriesId, ct),
            _ => SpaPage.NotFound
        };
    }

    private static bool Is(string segment, string literal) =>
        segment.Equals(literal, StringComparison.OrdinalIgnoreCase);

    private async Task<SpaPage> StandingsYearAsync(string yearText, CancellationToken ct)
    {
        if (!int.TryParse(yearText, out var year))
            return SpaPage.NotFound;

        return await _context.GrandPrixStandings.AnyAsync(s => s.Year == year, ct)
            ? SpaPage.Indexable
            : SpaPage.NotFound;
    }

    private async Task<SpaPage> SeriesAsync(string idText, CancellationToken ct)
    {
        if (!Guid.TryParse(idText, out var id))
            return SpaPage.NotFound;

        return await _context.RaceSeries.AnyAsync(s => s.RaceSeriesId == id, ct)
            ? SpaPage.Indexable
            : SpaPage.NotFound;
    }

    private async Task<SpaPage> RaceAsync(string idText, CancellationToken ct)
    {
        if (!Guid.TryParse(idText, out var id))
            return SpaPage.NotFound;

        return await _context.Races.AnyAsync(r => r.RaceId == id, ct)
            ? SpaPage.Indexable
            : SpaPage.NotFound;
    }

    /// <summary>
    /// A runner's profile, or (with <paramref name="seriesIdText"/>) their page for one series,
    /// which only exists when they have a result in it.
    /// </summary>
    private async Task<SpaPage> RunnerAsync(string idText, string? seriesIdText, CancellationToken ct)
    {
        if (!Guid.TryParse(idText, out var runnerId))
            return SpaPage.NotFound;

        var runner = await _context.Runners
            .Where(r => r.RunnerId == runnerId)
            .Select(r => new { r.DateOfBirth, r.EstimatedBirthYear })
            .FirstOrDefaultAsync(ct);
        if (runner is null)
            return SpaPage.NotFound;

        if (seriesIdText is not null)
        {
            if (!Guid.TryParse(seriesIdText, out var seriesId))
                return SpaPage.NotFound;

            var racedSeries = await _context.RaceResults.AnyAsync(
                rr => rr.RunnerId == runnerId && rr.Race.RaceVariant.RaceSeriesId == seriesId, ct);
            if (!racedSeries)
                return SpaPage.NotFound;
        }

        return SearchIndexing.IsRunnerIndexable(runner.DateOfBirth, runner.EstimatedBirthYear, SearchIndexing.Today)
            ? SpaPage.Indexable
            : SpaPage.NoIndexPage;
    }
}
