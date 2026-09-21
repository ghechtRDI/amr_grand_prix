using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Services.RaceStatistics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AmrGrandPrix.API.Tests.Services.RaceStatistics;

public class RaceStatisticsServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly RaceStatisticsService _service;
    private readonly Guid _seriesId = Guid.NewGuid();

    public RaceStatisticsServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _service = new RaceStatisticsService(_context);
    }

    private Runner AddRunner(string firstName, string lastName)
    {
        var runner = new Runner { RunnerId = Guid.NewGuid(), FirstName = firstName, LastName = lastName, Gender = Gender.Male };
        _context.Runners.Add(runner);
        return runner;
    }

    private Race AddRace(int year, DateOnly date)
    {
        var race = new Race
        {
            RaceId = Guid.NewGuid(),
            Name = "Test Race",
            Date = date,
            Year = year,
            RaceSeriesId = _seriesId,
            CourseVariant = "Standard"
        };
        _context.Races.Add(race);
        return race;
    }

    private void AddResult(Race race, Runner runner, TimeSpan time, string? ageCategory = null)
    {
        _context.RaceResults.Add(new RaceResult
        {
            ResultId = Guid.NewGuid(),
            RaceId = race.RaceId,
            RunnerId = runner.RunnerId,
            Time = time,
            Gender = Gender.Male,
            Status = ResultStatus.Finished,
            AgeCategory = ageCategory,
            UploadBatchId = Guid.NewGuid()
        });
    }

    [Fact]
    public async Task GetStatisticsAsync_RecordBrokenTwice_ReturnsHistoryOfBothHolders()
    {
        // Arrange: runner A sets a time in year 1, runner B beats it in year 2, and a slower
        // time in year 3 should not appear in the record history.
        var runnerA = AddRunner("Alice", "Anders");
        var runnerB = AddRunner("Bob", "Baker");
        var runnerC = AddRunner("Cara", "Chen");

        var race2020 = AddRace(2020, new DateOnly(2020, 7, 4));
        var race2021 = AddRace(2021, new DateOnly(2021, 7, 4));
        var race2022 = AddRace(2022, new DateOnly(2022, 7, 4));

        AddResult(race2020, runnerA, TimeSpan.FromMinutes(50));
        AddResult(race2021, runnerB, TimeSpan.FromMinutes(45));
        AddResult(race2022, runnerC, TimeSpan.FromMinutes(48)); // slower than the current record

        await _context.SaveChangesAsync();

        // Act
        var stats = await _service.GetStatisticsAsync(_seriesId, "Standard", Gender.Male);

        // Assert
        stats.CourseRecordHistory.Should().HaveCount(2);
        stats.CourseRecordHistory[0].RunnerName.Should().Be("Alice Anders");
        stats.CourseRecordHistory[1].RunnerName.Should().Be("Bob Baker");
    }

    [Fact]
    public async Task GetStatisticsAsync_Top20AllTime_OrdersByTimeAscending()
    {
        var runnerA = AddRunner("Alice", "Anders");
        var runnerB = AddRunner("Bob", "Baker");
        var race = AddRace(2020, new DateOnly(2020, 7, 4));

        AddResult(race, runnerA, TimeSpan.FromMinutes(50));
        AddResult(race, runnerB, TimeSpan.FromMinutes(45));
        await _context.SaveChangesAsync();

        var stats = await _service.GetStatisticsAsync(_seriesId, "Standard", Gender.Male);

        stats.Top20AllTime.Should().HaveCount(2);
        stats.Top20AllTime[0].RunnerName.Should().Be("Bob Baker");
        stats.Top20AllTime[1].RunnerName.Should().Be("Alice Anders");
    }

    [Fact]
    public async Task GetStatisticsAsync_AgeGroupRecords_ReturnsFastestPerCategory()
    {
        var runnerA = AddRunner("Alice", "Anders");
        var runnerB = AddRunner("Bob", "Baker");
        var race = AddRace(2020, new DateOnly(2020, 7, 4));

        AddResult(race, runnerA, TimeSpan.FromMinutes(50), ageCategory: "30-39");
        AddResult(race, runnerB, TimeSpan.FromMinutes(45), ageCategory: "30-39");
        await _context.SaveChangesAsync();

        var stats = await _service.GetStatisticsAsync(_seriesId, "Standard", Gender.Male);

        stats.AgeGroupRecords.Should().ContainSingle(r => r.AgeCategory == "30-39" && r.RunnerName == "Bob Baker");
    }

    [Fact]
    public async Task GetStatisticsAsync_DifferentCourseVariant_IsExcluded()
    {
        var runner = AddRunner("Alice", "Anders");
        var race = AddRace(2020, new DateOnly(2020, 7, 4));
        race.CourseVariant = "Uphill Only";
        AddResult(race, runner, TimeSpan.FromMinutes(30));
        await _context.SaveChangesAsync();

        var stats = await _service.GetStatisticsAsync(_seriesId, "Standard", Gender.Male);

        stats.Top20AllTime.Should().BeEmpty();
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
