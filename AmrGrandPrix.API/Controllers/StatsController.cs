using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models.DTOs;

namespace AmrGrandPrix.API.Controllers;

/// <summary>
/// Public site-wide statistics.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StatsController : ControllerBase
{
    internal const string SummaryCacheKey = "stats:summary";
    internal static readonly TimeSpan SummaryCacheDuration = TimeSpan.FromMinutes(5);

    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;

    public StatsController(ApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    /// <summary>
    /// Totals shown on the landing page: races and years with results, and athletes with at least
    /// one result. Cached briefly because it is fetched on every landing-page view.
    /// </summary>
    [HttpGet("summary")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SiteSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SiteSummaryDto>> GetSummary()
    {
        var summary = await _cache.GetOrCreateAsync(SummaryCacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = SummaryCacheDuration;
            return BuildSummaryAsync();
        });

        return Ok(summary);
    }

    private async Task<SiteSummaryDto> BuildSummaryAsync()
    {
        var raceYears = await _context.Races
            .Where(r => r.Results.Any())
            .Select(r => r.Year)
            .ToListAsync();

        var athleteCount = await _context.RaceResults
            .Select(r => r.RunnerId)
            .Distinct()
            .CountAsync();

        return new SiteSummaryDto
        {
            RaceCount = raceYears.Count,
            YearCount = raceYears.Distinct().Count(),
            FirstYear = raceYears.Count > 0 ? raceYears.Min() : null,
            LatestYear = raceYears.Count > 0 ? raceYears.Max() : null,
            AthleteCount = athleteCount
        };
    }
}
