using AmrGrandPrix.API.Controllers;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs;
using AmrGrandPrix.API.Services.GrandPrix;
using AmrGrandPrix.API.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AmrGrandPrix.API.Tests.Controllers;

/// <summary>
/// The points breakdown shows every runner's points per GP race, Overall then per age group.
/// </summary>
public class StandingsBreakdownTests : IDisposable
{
    private const int Year = 2026;

    private readonly ApplicationDbContext _context;
    private readonly GrandPrixCalculationService _gp;
    private readonly StandingsController _controller;

    public StandingsBreakdownTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        _context = new ApplicationDbContext(options);
        _gp = new GrandPrixCalculationService(_context, Mock.Of<ILogger<GrandPrixCalculationService>>());
        _controller = new StandingsController(_context, _gp, Mock.Of<ILogger<StandingsController>>());
    }

    public void Dispose() => _context.Dispose();

    private Race AddRace(string name, int month, bool isGrandPrix = true)
    {
        var race = new Race
        {
            RaceId = Guid.NewGuid(), RaceVariant = TestData.Variant(name), IsGrandPrixRace = isGrandPrix,
            Year = Year, Date = new DateOnly(Year, month, 1)
        };
        _context.Races.Add(race);
        return race;
    }

    private Runner AddRunner(string first, string last, Gender gender)
    {
        var runner = new Runner { RunnerId = Guid.NewGuid(), FirstName = first, LastName = last, Gender = gender };
        _context.Runners.Add(runner);
        return runner;
    }

    private void AddResult(Race race, Runner runner, int minutes, int age, bool isNewRecord = false) =>
        _context.RaceResults.Add(new RaceResult
        {
            ResultId = Guid.NewGuid(), RaceId = race.RaceId, RunnerId = runner.RunnerId,
            Time = TimeSpan.FromMinutes(minutes), Status = ResultStatus.Finished, Gender = runner.Gender,
            Age = age, IsNewRecord = isNewRecord, UploadBatchId = Guid.NewGuid()
        });

    private async Task<StandingsBreakdownResponse> GetBreakdownAsync(string gender = "female")
    {
        await _context.SaveChangesAsync();
        foreach (var raceId in await _context.Races.Where(r => r.IsGrandPrixRace).Select(r => r.RaceId).ToListAsync())
            await _gp.CalculateRacePointsAsync(raceId);
        await _gp.UpdateStandingsAsync(Year);

        var result = await _controller.GetBreakdown(Year, gender);
        return (StandingsBreakdownResponse)((OkObjectResult)result.Result!).Value!;
    }

    [Fact]
    public async Task GetBreakdown_ListsGrandPrixSeriesInDateOrder()
    {
        var late = AddRace("Veins of Gold", 9);
        AddRace("Not GP", 6, isGrandPrix: false);
        var early = AddRace("Crazy Lazy", 5);

        var response = await GetBreakdownAsync();

        response.Columns.Select(c => c.RaceIds.Single()).Should().Equal(early.RaceId, late.RaceId);
    }

    [Fact]
    public async Task GetBreakdown_MarksBest4AsCounted_AndShowsEveryScoringRace()
    {
        var runner = AddRunner("Shauna", "Severson", Gender.Female);
        var races = Enumerable.Range(1, 6).Select(m => AddRace($"Race {m}", m)).ToList();
        foreach (var race in races) AddResult(race, runner, 60, 35);
        // A faster runner in the first two races pushes Severson to 2nd (90) there
        var rival = AddRunner("Fast", "Rival", Gender.Female);
        AddResult(races[0], rival, 50, 35);
        AddResult(races[1], rival, 50, 35);

        var response = await GetBreakdownAsync();

        var row = response.Overall.Rows.Single(r => r.RunnerId == runner.RunnerId);
        row.Points.Should().HaveCount(6);
        row.Points.Values.Count(c => c.Counted).Should().Be(4);
        row.Points.Values.Where(c => c.Counted).Sum(c => c.Points).Should().Be(row.TotalPoints).And.Be(400);
        row.Points[races[0].RaceVariant.RaceSeriesId].Counted.Should().BeFalse();
        row.Points[races[1].RaceVariant.RaceSeriesId].Counted.Should().BeFalse();
        row.RacesCompleted.Should().Be(6);
    }

    [Fact]
    public async Task GetBreakdown_IncludesJuniorsInOverall_AndGroupsByAgeCategory()
    {
        var race = AddRace("Bird Ridge", 7);
        var junior = AddRunner("Rylee", "Ruggles", Gender.Female);
        var adult = AddRunner("Megan", "Neale", Gender.Female);
        AddResult(race, junior, 50, 16, isNewRecord: true);
        AddResult(race, adult, 55, 34);

        var response = await GetBreakdownAsync();

        response.Overall.Rows.Select(r => r.RunnerId).Should().Equal(junior.RunnerId, adult.RunnerId);
        var juniorCell = response.Overall.Rows[0].Points[race.RaceVariant.RaceSeriesId];
        juniorCell.Points.Should().Be(110);
        juniorCell.IsRecordBonus.Should().BeTrue();

        response.AgeGroups.Select(g => g.AgeCategory).Should().Equal("17 and Under", "30-39");
        response.AgeGroups[0].Rows.Single().RunnerId.Should().Be(junior.RunnerId);
        response.AgeGroups[0].Rows.Single().Points[race.RaceVariant.RaceSeriesId].Points.Should().Be(5);
    }

    [Fact]
    public async Task GetBreakdown_JuniorCourse_SharesSeriesColumn_AndScoresAgeGroupsOnly()
    {
        var hillClimb = AddRace("Bird Ridge", 6);
        var jacksRunVariant = TestData.AddVariant(hillClimb.RaceVariant.RaceSeries, "Jack's Run", isGrandPrixByDefault: true);
        jacksRunVariant.IsAgeGroupOnly = true;
        var jacksRun = new Race
        {
            RaceId = Guid.NewGuid(), RaceVariant = jacksRunVariant, IsGrandPrixRace = true,
            Year = Year, Date = hillClimb.Date
        };
        _context.Races.Add(jacksRun);

        var adult = AddRunner("Klaire", "Rhodes", Gender.Female);
        var kid = AddRunner("Nina", "Varnell", Gender.Female);
        AddResult(hillClimb, adult, 80, 25);
        AddResult(jacksRun, kid, 20, 11);

        var response = await GetBreakdownAsync();

        var column = response.Columns.Should().ContainSingle().Subject;
        column.Name.Should().Be("Bird Ridge");
        column.RaceIds.Should().BeEquivalentTo([hillClimb.RaceId, jacksRun.RaceId]);

        response.Overall.Rows.Should().ContainSingle().Which.RunnerId.Should().Be(adult.RunnerId);

        var juniors = response.AgeGroups.Single(g => g.AgeCategory == "17 and Under");
        var cell = juniors.Rows.Single().Points[column.RaceSeriesId];
        cell.RaceId.Should().Be(jacksRun.RaceId);
        cell.Points.Should().Be(5);
    }

    [Fact]
    public async Task GetBreakdown_OnlyIncludesRequestedGender()
    {
        var race = AddRace("Mat Peak", 8);
        AddResult(race, AddRunner("A", "Woman", Gender.Female), 60, 30);
        AddResult(race, AddRunner("A", "Man", Gender.Male), 55, 30);

        var response = await GetBreakdownAsync("male");

        response.Gender.Should().Be(Gender.Male);
        response.Overall.Division.Should().Be(Division.OpenMale);
        response.Overall.Rows.Should().ContainSingle().Which.RunnerName.Should().Be("A Man");
    }

    [Fact]
    public async Task GetBreakdown_RejectsInvalidGender()
    {
        var result = await _controller.GetBreakdown(Year, "other");

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }
}
