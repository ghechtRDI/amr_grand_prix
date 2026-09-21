using AmrGrandPrix.API.Models;

namespace AmrGrandPrix.API.Services.RaceStatistics;

public interface IRaceStatisticsService
{
    /// <summary>
    /// Computes all-time statistics for one course variant/gender within a race series:
    /// the top 20 finish times ever, the history of who has held the course record, and the
    /// current record holder per age category.
    /// </summary>
    Task<RaceSeriesStatisticsDto> GetStatisticsAsync(Guid raceSeriesId, string? courseVariant, Gender gender);
}

public class RaceSeriesStatisticsDto
{
    public string? CourseVariant { get; set; }
    public Gender Gender { get; set; }
    public List<TimeResultDto> Top20AllTime { get; set; } = new();
    public List<CourseRecordEventDto> CourseRecordHistory { get; set; } = new();
    public List<AgeGroupRecordDto> AgeGroupRecords { get; set; } = new();
}

public class TimeResultDto
{
    public string RunnerName { get; set; } = string.Empty;
    public TimeSpan Time { get; set; }
    public int Year { get; set; }
    public DateOnly RaceDate { get; set; }
}

public class CourseRecordEventDto
{
    public string RunnerName { get; set; } = string.Empty;
    public TimeSpan Time { get; set; }
    public DateOnly RaceDate { get; set; }
}

public class AgeGroupRecordDto
{
    public string AgeCategory { get; set; } = string.Empty;
    public string RunnerName { get; set; } = string.Empty;
    public TimeSpan Time { get; set; }
    public DateOnly RaceDate { get; set; }
}
