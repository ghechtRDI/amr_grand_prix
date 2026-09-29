using Microsoft.EntityFrameworkCore;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;

namespace AmrGrandPrix.API.Services.RaceStatistics;

public class RaceStatisticsService : IRaceStatisticsService
{
    private readonly ApplicationDbContext _context;

    public RaceStatisticsService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RaceSeriesStatisticsDto> GetStatisticsAsync(Guid raceVariantId, Gender gender)
    {
        var results = await _context.RaceResults
            .Include(r => r.Race)
            .Include(r => r.Runner)
            .Where(r => r.Race.RaceVariantId == raceVariantId &&
                        r.Gender == gender &&
                        r.Status == ResultStatus.Finished &&
                        r.Time != null)
            .ToListAsync();

        var dto = new RaceSeriesStatisticsDto
        {
            RaceVariantId = raceVariantId,
            Gender = gender,
            Top20AllTime = results
                .OrderBy(r => r.Time)
                .Take(GrandPrixConstants.OpenDivisionTopFinishers)
                .Select(r => new TimeResultDto
                {
                    RunnerName = r.Runner.FullName,
                    Time = r.Time!.Value,
                    Year = r.Race.Year,
                    RaceDate = r.Race.Date
                })
                .ToList(),
            CourseRecordHistory = BuildRecordHistory(results),
            AgeGroupRecords = results
                .Where(r => !string.IsNullOrEmpty(r.AgeCategory))
                .GroupBy(r => r.AgeCategory!)
                .Select(g =>
                {
                    var best = g.OrderBy(r => r.Time).First();
                    return new AgeGroupRecordDto
                    {
                        AgeCategory = g.Key,
                        RunnerName = best.Runner.FullName,
                        Time = best.Time!.Value,
                        RaceDate = best.Race.Date
                    };
                })
                .OrderBy(r => GrandPrixConstants.AgeCategories.FindIndex(c => c.Name == r.AgeCategory))
                .ToList()
        };

        return dto;
    }

    /// <summary>
    /// Sweeps results in chronological order, emitting an entry each time a new fastest-ever time
    /// appears — the historical timeline of who has held the course record.
    /// </summary>
    private static List<CourseRecordEventDto> BuildRecordHistory(List<RaceResult> results)
    {
        var history = new List<CourseRecordEventDto>();
        TimeSpan? currentRecord = null;

        foreach (var result in results.OrderBy(r => r.Race.Date))
        {
            if (currentRecord.HasValue && result.Time >= currentRecord.Value)
                continue;

            currentRecord = result.Time;
            history.Add(new CourseRecordEventDto
            {
                RunnerName = result.Runner.FullName,
                Time = result.Time!.Value,
                RaceDate = result.Race.Date
            });
        }

        return history;
    }
}
