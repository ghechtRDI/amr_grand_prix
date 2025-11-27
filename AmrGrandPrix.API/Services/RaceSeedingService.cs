using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AmrGrandPrix.API.Services;

public class RaceSeedingService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RaceSeedingService> _logger;

    public RaceSeedingService(ApplicationDbContext context, ILogger<RaceSeedingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Seeds the database with the 9 Grand Prix races for a given year
    /// </summary>
    public async Task SeedGrandPrixRacesAsync(int year)
    {
        _logger.LogInformation("Seeding Grand Prix races for year {Year}", year);

        // Check if races for this year already exist
        var existingRaces = await _context.Races
            .Where(r => r.Year == year && r.IsGrandPrixRace)
            .CountAsync();

        if (existingRaces > 0)
        {
            _logger.LogInformation("Grand Prix races for year {Year} already exist. Skipping seed.", year);
            return;
        }

        var races = GetGrandPrixRaceTemplates(year);

        await _context.Races.AddRangeAsync(races);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Successfully seeded {Count} Grand Prix races for year {Year}", races.Count, year);
    }

    /// <summary>
    /// Gets the template for all 9 Grand Prix races for a given year
    /// </summary>
    private List<Race> GetGrandPrixRaceTemplates(int year)
    {
        var races = new List<Race>
        {
            new Race
            {
                RaceId = Guid.NewGuid(),
                Name = "Crazy Lazy",
                IsGrandPrixRace = true,
                GrandPrixRaceOrder = 1,
                Date = new DateTime(year, 4, 15, 0, 0, 0, DateTimeKind.Utc), // Approximate - usually mid-April
                Year = year,
                Location = "Anchorage, AK",
                CreatedAt = DateTime.UtcNow
            },
            new Race
            {
                RaceId = Guid.NewGuid(),
                Name = "Kal's Knoya Ridge Run",
                IsGrandPrixRace = true,
                GrandPrixRaceOrder = 2,
                Date = new DateTime(year, 5, 1, 0, 0, 0, DateTimeKind.Utc), // Approximate - usually early May
                Year = year,
                CourseVariant = "Full Monty",
                Location = "Girdwood, AK",
                CreatedAt = DateTime.UtcNow
            },
            new Race
            {
                RaceId = Guid.NewGuid(),
                Name = "Government Peak Climb",
                IsGrandPrixRace = true,
                GrandPrixRaceOrder = 3,
                Date = new DateTime(year, 5, 20, 0, 0, 0, DateTimeKind.Utc), // Approximate - usually late May
                Year = year,
                CourseVariant = "Up-and-Down",
                Location = "Hatcher Pass, AK",
                CreatedAt = DateTime.UtcNow
            },
            new Race
            {
                RaceId = Guid.NewGuid(),
                Name = "Robert Spurr Memorial Hill Climb (Bird Ridge)",
                IsGrandPrixRace = true,
                GrandPrixRaceOrder = 4,
                Date = new DateTime(year, 6, 1, 0, 0, 0, DateTimeKind.Utc), // Approximate - usually early June
                Year = year,
                Location = "Bird Creek, AK",
                CreatedAt = DateTime.UtcNow
            },
            new Race
            {
                RaceId = Guid.NewGuid(),
                Name = "Juneau Ridge Race",
                IsGrandPrixRace = true,
                GrandPrixRaceOrder = 5,
                Date = new DateTime(year, 6, 15, 0, 0, 0, DateTimeKind.Utc), // Approximate - usually mid-June
                Year = year,
                Location = "Juneau, AK",
                CreatedAt = DateTime.UtcNow
            },
            new Race
            {
                RaceId = Guid.NewGuid(),
                Name = "Mount Marathon Race",
                IsGrandPrixRace = true,
                GrandPrixRaceOrder = 6,
                Date = new DateTime(year, 7, 4, 0, 0, 0, DateTimeKind.Utc), // Always July 4th
                Year = year,
                Location = "Seward, AK",
                CreatedAt = DateTime.UtcNow
            },
            new Race
            {
                RaceId = Guid.NewGuid(),
                Name = "Cirque Series Alyeska",
                IsGrandPrixRace = true,
                GrandPrixRaceOrder = 7,
                Date = new DateTime(year, 7, 20, 0, 0, 0, DateTimeKind.Utc), // Approximate - usually late July
                Year = year,
                Location = "Girdwood, AK",
                CreatedAt = DateTime.UtcNow
            },
            new Race
            {
                RaceId = Guid.NewGuid(),
                Name = "Matanuska Peak Challenge",
                IsGrandPrixRace = true,
                GrandPrixRaceOrder = 8,
                Date = new DateTime(year, 8, 1, 0, 0, 0, DateTimeKind.Utc), // Approximate - usually early August
                Year = year,
                Location = "Palmer, AK",
                CreatedAt = DateTime.UtcNow
            },
            new Race
            {
                RaceId = Guid.NewGuid(),
                Name = "Veins of Gold",
                IsGrandPrixRace = true,
                GrandPrixRaceOrder = 9,
                Date = new DateTime(year, 9, 1, 0, 0, 0, DateTimeKind.Utc), // Approximate - usually early September
                Year = year,
                Location = "Juneau, AK",
                CreatedAt = DateTime.UtcNow
            }
        };

        return races;
    }
}
