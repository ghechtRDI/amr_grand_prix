using System.Globalization;
using Microsoft.EntityFrameworkCore;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs;

namespace AmrGrandPrix.API.Services.Seo;

/// <summary>How the server should answer a request for a client-side (SPA) route, and the
/// metadata to fill into the app shell for crawlers and link previews.</summary>
public sealed record SpaPage
{
    public int StatusCode { get; init; } = StatusCodes.Status200OK;
    public bool NoIndex { get; init; }

    /// <summary>Page title without the site suffix; null for the site default.</summary>
    public string? Title { get; init; }

    /// <summary>A title used verbatim (the landing page).</summary>
    public string? RawTitle { get; init; }

    public string? Description { get; init; }

    /// <summary>Path of the page's canonical URL; null for noindex pages.</summary>
    public string? CanonicalPath { get; init; }

    /// <summary>schema.org objects, each rendered as its own JSON-LD script.</summary>
    public IReadOnlyList<object> JsonLd { get; init; } = [];

    public string FullTitle => RawTitle ?? (Title is null ? SiteMeta.DefaultTitle : $"{Title} | {SiteMeta.SiteName}");

    public static readonly SpaPage NotFound = new()
    {
        StatusCode = StatusCodes.Status404NotFound, NoIndex = true, Title = "Page Not Found"
    };

    public static SpaPage Private(string title) => new() { NoIndex = true, Title = title };
}

/// <summary>
/// Mirrors the client's route table (<c>AmrGrandPrix.Client/src/App.jsx</c>) so the app shell is
/// served with a real status — 404 for unknown paths and for IDs that don't exist, noindex for
/// private pages and for runners under 18 — and with the title, description, canonical, and
/// JSON-LD the page will show, for crawlers and link previews that don't run JavaScript.
/// Matching is case-insensitive and ignores a trailing slash, like React Router.
/// </summary>
public class SpaPageResolver
{
    private static readonly Dictionary<string, SpaPage> StaticPages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["race-results"] = new()
        {
            Title = "Race Results",
            Description = "Browse results from Alaska mountain and trail races by year or by race series, back through every season on record.",
            CanonicalPath = "/race-results"
        },
        ["athletes"] = new()
        {
            Title = "Athletes",
            Description = "Search every runner in the Alaska Mountain Runners results archive and see their race history and best Grand Prix season.",
            CanonicalPath = "/athletes"
        },
        ["report"] = new()
        {
            Title = "Report an Issue",
            Description = "Report a mistake in Alaska Mountain Runners race results or Grand Prix standings.",
            CanonicalPath = "/report"
        },
        ["privacy"] = new()
        {
            Title = "Privacy",
            Description = "What the Alaska Mountain Runners results site collects, including Google Analytics, and how it is used.",
            CanonicalPath = "/privacy"
        },
        ["login"] = SpaPage.Private("Login"),
        ["register"] = SpaPage.Private("Register"),
        ["confirm-email"] = SpaPage.Private("Confirm Email"),
        ["forgot-password"] = SpaPage.Private("Forgot Password"),
        ["reset-password"] = SpaPage.Private("Reset Password"),
        ["unauthorized"] = SpaPage.Private("Unauthorized"),
        ["profile"] = SpaPage.Private("Your Profile"),
        ["admin/results"] = SpaPage.Private("Results Management"),
        ["admin/results/upload"] = SpaPage.Private("Upload Results"),
        ["admin/runners"] = SpaPage.Private("Racer Management"),
        ["admin/users"] = SpaPage.Private("User Management"),
    };

    /// <summary>Shown in place of a minor's name, so it doesn't appear in link previews either.</summary>
    private static readonly SpaPage MinorRunnerPage = new()
    {
        NoIndex = true,
        Title = "Runner Profile",
        Description = SiteMeta.DefaultDescription
    };

    private readonly ApplicationDbContext _context;

    public SpaPageResolver(ApplicationDbContext context) => _context = context;

    /// <param name="path">The request path.</param>
    /// <param name="siteUrl">The public origin, for absolute URLs in JSON-LD.</param>
    public async Task<SpaPage> ResolveAsync(string path, string siteUrl, CancellationToken ct = default)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
            return Landing(siteUrl);
        if (StaticPages.TryGetValue(string.Join('/', segments), out var page))
            return page;

        return segments switch
        {
            [var s] when Is(s, "standings") => await StandingsAsync(null, ct),
            [var s, var year] when Is(s, "standings") => await StandingsAsync(year, ct),
            [var s, var id] when Is(s, "race-series") => await SeriesAsync(id, ct),
            [var s, var id, var r] when Is(s, "races") && Is(r, "results") => await RaceAsync(id, siteUrl, ct),
            [var s, var id] when Is(s, "runners") => await RunnerAsync(id, null, ct),
            [var s, var id, var se, var seriesId] when Is(s, "runners") && Is(se, "series") =>
                await RunnerAsync(id, seriesId, ct),
            _ => SpaPage.NotFound
        };
    }

    private static bool Is(string segment, string literal) =>
        segment.Equals(literal, StringComparison.OrdinalIgnoreCase);

    private static SpaPage Landing(string siteUrl) => new()
    {
        RawTitle = SiteMeta.DefaultTitle,
        Description = SiteMeta.DefaultDescription,
        CanonicalPath = "/",
        JsonLd =
        [
            new Dictionary<string, object>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "Organization",
                ["name"] = SiteMeta.SiteName,
                ["url"] = SiteMeta.ClubUrl,
                ["logo"] = $"{siteUrl}/favicon.svg"
            },
            new Dictionary<string, object>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "WebSite",
                ["name"] = SiteMeta.DefaultTitle,
                ["url"] = $"{siteUrl}/",
                ["publisher"] = new Dictionary<string, object> { ["@type"] = "Organization", ["name"] = SiteMeta.SiteName }
            }
        ]
    };

    /// <summary>
    /// A standings year, or (with no year) the latest year with standings, which is what
    /// <c>/standings</c> opens and so its canonical URL.
    /// </summary>
    private async Task<SpaPage> StandingsAsync(string? yearText, CancellationToken ct)
    {
        int year;
        if (yearText is null)
        {
            var latest = await _context.GrandPrixStandings.MaxAsync(s => (int?)s.Year, ct);
            if (latest is null)
                return new SpaPage { Title = "Grand Prix Standings", CanonicalPath = "/standings" };
            year = latest.Value;
        }
        else if (!int.TryParse(yearText, out year)
                 || !await _context.GrandPrixStandings.AnyAsync(s => s.Year == year, ct))
        {
            return SpaPage.NotFound;
        }

        var leaders = await _context.GrandPrixStandings
            .Where(s => s.Year == year && s.AgeCategory == null && s.Rank == 1)
            .Select(s => new { s.Division, s.Runner.FirstName, s.Runner.LastName })
            .ToListAsync(ct);
        var finalized = await _context.GrandPrixSeasons.AnyAsync(s => s.Year == year && s.IsFinalized, ct);

        var leaderText = string.Join(", ", new[]
            {
                (Division.OpenFemale, "women"), (Division.OpenMale, "men"), (Division.OpenNonbinary, "nonbinary")
            }
            .Select(d => (Names: leaders.Where(l => l.Division == d.Item1).Select(l => $"{l.FirstName} {l.LastName}").Order().ToList(), Label: d.Item2))
            .Where(d => d.Names.Count > 0)
            .Select(d => $"{string.Join(" and ", d.Names)} ({d.Label})"));

        var description =
            $"{year} AMR Grand Prix standings: Open and age-division points for every runner, the race-by-race breakdown, and Run the Gamut finishers.";
        if (leaderText.Length > 0)
            description += $" {(finalized ? "Champions" : "Leading")}: {leaderText}.";

        return new SpaPage
        {
            Title = $"{year} Grand Prix Standings",
            Description = description,
            CanonicalPath = $"/standings/{year}"
        };
    }

    private async Task<SpaPage> SeriesAsync(string idText, CancellationToken ct)
    {
        if (!Guid.TryParse(idText, out var id))
            return SpaPage.NotFound;

        var series = await _context.RaceSeries
            .Where(s => s.RaceSeriesId == id)
            .Select(s => new
            {
                s.Name,
                Variants = s.Variants.OrderBy(v => v.DisplayOrder).ThenBy(v => v.Name).Select(v => v.Name).ToList()
            })
            .FirstOrDefaultAsync(ct);
        if (series is null)
            return SpaPage.NotFound;

        var description = $"Every year of {series.Name} results, all-time top times, and course records.";
        if (series.Variants.Count > 1)
            description += $" Courses: {string.Join(", ", series.Variants)}.";

        return new SpaPage
        {
            Title = $"{series.Name} Results and Records",
            Description = description,
            CanonicalPath = $"/race-series/{id}"
        };
    }

    private async Task<SpaPage> RaceAsync(string idText, string siteUrl, CancellationToken ct)
    {
        if (!Guid.TryParse(idText, out var id))
            return SpaPage.NotFound;

        var race = await _context.Races
            .Where(r => r.RaceId == id)
            .Select(r => new
            {
                SeriesName = r.RaceVariant.RaceSeries.Name,
                VariantName = r.RaceVariant.Name,
                VariantCount = r.RaceVariant.RaceSeries.Variants.Count,
                r.Year,
                r.Date,
                r.Location,
                ResultCount = r.Results.Count
            })
            .FirstOrDefaultAsync(ct);
        if (race is null)
            return SpaPage.NotFound;

        var name = RaceProjections.DisplayName(race.SeriesName, race.VariantName, race.VariantCount);
        var label = $"{name} {race.Year}";
        var date = race.Date.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);
        var canonicalPath = $"/races/{id}/results";
        var isoDate = race.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return new SpaPage
        {
            Title = $"{label} Results",
            Description = $"Full results for {label}, run {date}: {race.ResultCount} results with times, places, and age groups.",
            CanonicalPath = canonicalPath,
            JsonLd =
            [
                new Dictionary<string, object>
                {
                    ["@context"] = "https://schema.org",
                    ["@type"] = "SportsEvent",
                    ["name"] = label,
                    ["sport"] = "Mountain running",
                    ["startDate"] = isoDate,
                    ["endDate"] = isoDate,
                    ["eventStatus"] = "https://schema.org/EventScheduled",
                    ["eventAttendanceMode"] = "https://schema.org/OfflineEventAttendanceMode",
                    ["location"] = new Dictionary<string, object>
                    {
                        ["@type"] = "Place",
                        ["name"] = race.Location ?? race.SeriesName,
                        ["address"] = new Dictionary<string, object>
                        {
                            ["@type"] = "PostalAddress", ["addressRegion"] = "AK", ["addressCountry"] = "US"
                        }
                    },
                    ["image"] = siteUrl + SiteMeta.OgImagePath,
                    ["url"] = siteUrl + canonicalPath
                }
            ]
        };
    }

    /// <summary>
    /// A runner's profile, or (with <paramref name="seriesIdText"/>) their page for one series,
    /// which only exists when they have a result in it. Minors get a generic, noindex page.
    /// </summary>
    private async Task<SpaPage> RunnerAsync(string idText, string? seriesIdText, CancellationToken ct)
    {
        if (!Guid.TryParse(idText, out var runnerId))
            return SpaPage.NotFound;

        var runner = await _context.Runners
            .Where(r => r.RunnerId == runnerId)
            .Select(r => new { r.FirstName, r.LastName, r.DateOfBirth, r.EstimatedDobLatest, ResultCount = r.Results.Count })
            .FirstOrDefaultAsync(ct);
        if (runner is null)
            return SpaPage.NotFound;

        string? seriesName = null;
        Guid seriesId = default;
        if (seriesIdText is not null)
        {
            if (!Guid.TryParse(seriesIdText, out seriesId))
                return SpaPage.NotFound;

            seriesName = await _context.RaceResults
                .Where(rr => rr.RunnerId == runnerId && rr.Race.RaceVariant.RaceSeriesId == seriesId)
                .Select(rr => rr.Race.RaceVariant.RaceSeries.Name)
                .FirstOrDefaultAsync(ct);
            if (seriesName is null)
                return SpaPage.NotFound;
        }

        if (!SearchIndexing.IsRunnerIndexable(runner.DateOfBirth, runner.EstimatedDobLatest, SearchIndexing.Today))
            return MinorRunnerPage;

        var fullName = $"{runner.FirstName} {runner.LastName}";
        if (seriesName is not null)
        {
            return new SpaPage
            {
                Title = $"{fullName} – {seriesName} Results",
                Description = $"{fullName}'s {seriesName} results, year by year, with finish times charted by course.",
                CanonicalPath = $"/runners/{runnerId}/series/{seriesId}"
            };
        }

        return new SpaPage
        {
            Title = $"{fullName} – Race Results",
            Description = $"{fullName}'s Alaska mountain race results: {runner.ResultCount} {(runner.ResultCount == 1 ? "race" : "races")}, personal bests by course, and Grand Prix history.",
            CanonicalPath = $"/runners/{runnerId}"
        };
    }
}
