using System.Globalization;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using AmrGrandPrix.API.Data;

namespace AmrGrandPrix.API.Services.Seo;

/// <summary>
/// Builds <c>/sitemap.xml</c> from the database: the landing and listing pages, each Grand Prix
/// standings year, and every series, race, and indexable runner (see <see cref="SearchIndexing"/>)
/// that has results. <c>lastmod</c> is when the newest result behind each page was saved.
/// </summary>
public class SitemapBuilder
{
    private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    private readonly ApplicationDbContext _context;

    public SitemapBuilder(ApplicationDbContext context) => _context = context;

    public async Task<string> BuildAsync(string baseUrl, DateOnly today, CancellationToken ct = default)
    {
        baseUrl = baseUrl.TrimEnd('/');

        // Newest result per race, with what each race page rolls up into
        var races = await _context.RaceResults
            .GroupBy(rr => rr.RaceId)
            .Select(g => new { RaceId = g.Key, LastModified = g.Max(rr => rr.CreatedAt) })
            .Join(_context.Races, g => g.RaceId, r => r.RaceId, (g, r) => new
            {
                g.RaceId,
                g.LastModified,
                r.Year,
                r.RaceVariant.RaceSeriesId
            })
            .ToListAsync(ct);

        var runners = await _context.RaceResults
            .GroupBy(rr => rr.RunnerId)
            .Select(g => new { RunnerId = g.Key, LastModified = g.Max(rr => rr.CreatedAt) })
            .Join(_context.Runners, g => g.RunnerId, r => r.RunnerId, (g, r) => new
            {
                g.RunnerId,
                g.LastModified,
                r.DateOfBirth,
                r.EstimatedBirthYear
            })
            .ToListAsync(ct);

        var standingsYears = await _context.GrandPrixStandings
            .Select(s => s.Year)
            .Distinct()
            .ToListAsync(ct);

        DateTime? siteLastModified = races.Count > 0 ? races.Max(r => r.LastModified) : null;
        var entries = new List<(string Path, DateTime? LastModified)>
        {
            ("/", siteLastModified),
            ("/race-results", siteLastModified),
            ("/athletes", siteLastModified)
        };

        entries.AddRange(standingsYears
            .OrderByDescending(y => y)
            .Select(year => ($"/standings/{year}", races.Where(r => r.Year == year).Select(r => (DateTime?)r.LastModified).Max())));

        entries.AddRange(races
            .GroupBy(r => r.RaceSeriesId)
            .OrderBy(g => g.Key)
            .Select(g => ($"/race-series/{g.Key}", (DateTime?)g.Max(r => r.LastModified))));

        entries.AddRange(races
            .OrderBy(r => r.RaceId)
            .Select(r => ($"/races/{r.RaceId}/results", (DateTime?)r.LastModified)));

        entries.AddRange(runners
            .Where(r => SearchIndexing.IsRunnerIndexable(r.DateOfBirth, r.EstimatedBirthYear, today))
            .OrderBy(r => r.RunnerId)
            .Select(r => ($"/runners/{r.RunnerId}", (DateTime?)r.LastModified)));

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(Ns + "urlset",
                entries.Select(e => new XElement(Ns + "url",
                    new XElement(Ns + "loc", baseUrl + e.Path),
                    e.LastModified is { } lm
                        ? new XElement(Ns + "lastmod", lm.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                        : null))));

        return doc.Declaration + "\n" + doc.ToString(SaveOptions.DisableFormatting);
    }
}
