using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AmrGrandPrix.API.Services;

/// <summary>
/// Seeds the catalog of known race series and their course variants. Idempotent: series are
/// matched by name and variants by name/alias, so existing rows are never duplicated or
/// overwritten. Races (yearly runnings) are not seeded — they're created when results are uploaded.
/// </summary>
public class RaceSeedingService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RaceSeedingService> _logger;

    public RaceSeedingService(ApplicationDbContext context, ILogger<RaceSeedingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    private record VariantTemplate(string Name, bool IsGrandPrix, params string[] Aliases);

    private record SeriesTemplate(string Name, params VariantTemplate[] Variants);

    private static readonly SeriesTemplate[] Catalog =
    [
        new("Crazy Lazy", new VariantTemplate(RaceVariant.StandardName, true)),
        new("Kal's Knoya Ridge Run",
            // Full Monty is the GP race unless snow forces the Dome to be the GP race that year
            new VariantTemplate("Full Monty", true),
            new VariantTemplate("Dome", false, "Original", "Dome Finish"),
            new VariantTemplate("Happy Trails", false, "Happy Trail")),
        new("Government Peak Climb",
            new VariantTemplate("Round Trip", true, "Up-and-Down", "Up and Down"),
            new VariantTemplate("Uphill Only", false, "Up Hill Only")),
        new("Blueberry Rampage",
            new VariantTemplate("Full Mountain", true, "Adult - Full Mountain"),
            new VariantTemplate("Junior Blueberry Knoll", true, "Junior - Blueberry Knoll"),
            new VariantTemplate("Young at Heart Blueberry Knoll", false, "Adult - Young at Heart Blueberry Knoll")),
        new("Bird Ridge",
            new VariantTemplate("Hill Climb", true, "Robert Spurr Memorial Hill Climb"),
            new VariantTemplate("Jack's Run", true)),
        new("Juneau Ridge Race", new VariantTemplate(RaceVariant.StandardName, true)),
        new("Mount Marathon Race",
            new VariantTemplate("Adult", true),
            new VariantTemplate("Junior", true, "Juniors")),
        new("Crow Pass Crossing", new VariantTemplate(RaceVariant.StandardName, false)),
        new("Alyeska Cirque Series", new VariantTemplate(RaceVariant.StandardName, true)),
        new("Matanuska Peak Challenge", new VariantTemplate(RaceVariant.StandardName, true)),
        new("Veins of Gold", new VariantTemplate(RaceVariant.StandardName, true)),
    ];

    public async Task SeedRaceCatalogAsync()
    {
        var existing = await _context.RaceSeries.Include(s => s.Variants).ToListAsync();
        var seriesCreated = 0;
        var variantsCreated = 0;

        foreach (var template in Catalog)
        {
            var series = existing.FirstOrDefault(s => string.Equals(s.Name, template.Name, StringComparison.OrdinalIgnoreCase));
            if (series == null)
            {
                series = new RaceSeries { RaceSeriesId = Guid.NewGuid(), Name = template.Name, CreatedAt = DateTime.UtcNow };
                _context.RaceSeries.Add(series);
                seriesCreated++;
            }

            for (var i = 0; i < template.Variants.Length; i++)
            {
                var v = template.Variants[i];
                if (series.Variants.Any(existingVariant => existingVariant.Matches(v.Name)))
                    continue;

                series.Variants.Add(new RaceVariant
                {
                    RaceVariantId = Guid.NewGuid(),
                    RaceSeriesId = series.RaceSeriesId,
                    Name = v.Name,
                    Aliases = v.Aliases.ToList(),
                    IsGrandPrixByDefault = v.IsGrandPrix,
                    DisplayOrder = i,
                    CreatedAt = DateTime.UtcNow
                });
                variantsCreated++;
            }
        }

        if (seriesCreated + variantsCreated == 0)
            return;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Seeded {SeriesCount} race series and {VariantCount} variants", seriesCreated, variantsCreated);
    }
}
