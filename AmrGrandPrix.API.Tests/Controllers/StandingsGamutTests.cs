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
/// "Run the Gamut" lists runners who completed 7+ of the year's GP events, scoring or not.
/// </summary>
public class StandingsGamutTests : IDisposable
{
    private const int Year = 2026;

    private readonly ApplicationDbContext _context;
    private readonly StandingsController _controller;

    public StandingsGamutTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        _context = new ApplicationDbContext(options);
        var gp = new GrandPrixCalculationService(_context, Mock.Of<ILogger<GrandPrixCalculationService>>());
        _controller = new StandingsController(_context, gp, Mock.Of<ILogger<StandingsController>>());
    }

    public void Dispose() => _context.Dispose();

    private Race AddRace(RaceVariant variant, int month, bool isGrandPrix = true)
    {
        var race = new Race
        {
            RaceId = Guid.NewGuid(), RaceVariant = variant, IsGrandPrixRace = isGrandPrix,
            Year = Year, Date = new DateOnly(Year, month, 1)
        };
        _context.Races.Add(race);
        return race;
    }

    /// <summary>One GP race per series, in month order.</summary>
    private List<Race> AddGrandPrixSeries(int count) =>
        Enumerable.Range(1, count).Select(i => AddRace(TestData.Variant($"Series {i}"), i)).ToList();

    private Runner AddRunner(string first, string last, Gender gender = Gender.Female)
    {
        var runner = new Runner { RunnerId = Guid.NewGuid(), FirstName = first, LastName = last, Gender = gender };
        _context.Runners.Add(runner);
        return runner;
    }

    private void AddResult(Race race, Runner runner, ResultStatus status = ResultStatus.Finished) =>
        _context.RaceResults.Add(new RaceResult
        {
            ResultId = Guid.NewGuid(), RaceId = race.RaceId, RunnerId = runner.RunnerId,
            Status = status, Gender = runner.Gender, UploadBatchId = Guid.NewGuid()
        });

    private async Task<GamutResponse> GetGamutAsync()
    {
        await _context.SaveChangesAsync();
        var result = await _controller.GetGamut(Year);
        return (GamutResponse)((OkObjectResult)result.Result!).Value!;
    }

    [Fact]
    public async Task GetGamut_ListsRunnersWithSevenOrMoreEvents_MostEventsFirst()
    {
        var races = AddGrandPrixSeries(9);
        var nine = AddRunner("Nine", "Zed");
        var seven = AddRunner("Seven", "Able");
        var six = AddRunner("Six", "Baker");
        foreach (var race in races) AddResult(race, nine);
        foreach (var race in races.Take(7)) AddResult(race, seven);
        foreach (var race in races.Take(6)) AddResult(race, six);

        var gamut = await GetGamutAsync();

        gamut.MinEvents.Should().Be(7);
        gamut.Columns.Should().HaveCount(9);
        gamut.Runners.Select(r => r.RunnerName).Should().Equal("Nine Zed", "Seven Able");
        gamut.Runners[1].EventsCompleted.Should().Be(7);
        gamut.Runners[1].CompletedSeriesIds.Should().Equal(races.Take(7).Select(r => r.RaceVariant.RaceSeriesId));
    }

    [Fact]
    public async Task GetGamut_CountsFlaggedNonGrandPrixCourse_ButNotOtherCoursesDnfsOrNonGrandPrixSeries()
    {
        var races = AddGrandPrixSeries(6);
        var knoya = races[0].RaceVariant.RaceSeries;
        var dome = AddRace(TestData.AddVariant(knoya, "Dome"), 1, isGrandPrix: false);
        dome.RaceVariant.CountsTowardGamut = true;
        var happyTrails = AddRace(TestData.AddVariant(knoya, "Happy Trails"), 1, isGrandPrix: false);
        var crowPass = AddRace(TestData.Variant("Crow Pass Crossing"), 7, isGrandPrix: false);
        var lastGp = AddRace(TestData.Variant("Veins of Gold"), 9);

        // Dome stands in for Knoya; plus five more GP finishes and a DNF at the 7th event
        var domeRunner = AddRunner("Dome", "Runner");
        AddResult(dome, domeRunner);
        foreach (var race in races.Skip(1)) AddResult(race, domeRunner);
        AddResult(lastGp, domeRunner);

        var trailsRunner = AddRunner("Trails", "Runner");
        AddResult(happyTrails, trailsRunner);
        foreach (var race in races.Skip(1)) AddResult(race, trailsRunner);
        AddResult(lastGp, trailsRunner);

        var crowRunner = AddRunner("Crow", "Runner");
        foreach (var race in races) AddResult(race, crowRunner);
        AddResult(crowPass, crowRunner);

        var dnfRunner = AddRunner("Dnf", "Runner");
        foreach (var race in races) AddResult(race, dnfRunner);
        AddResult(lastGp, dnfRunner, ResultStatus.DNF);

        var gamut = await GetGamutAsync();

        gamut.Columns.Should().HaveCount(7, "Crow Pass has no GP race this year");
        gamut.Runners.Should().ContainSingle().Which.RunnerName.Should().Be("Dome Runner");
    }

    [Fact]
    public async Task GetGamut_TwoCoursesInOneSeries_CountAsOneEvent()
    {
        var races = AddGrandPrixSeries(6);
        var juniorCourse = AddRace(TestData.AddVariant(races[0].RaceVariant.RaceSeries, "Junior"), 1);
        var runner = AddRunner("Double", "Dipper");
        foreach (var race in races) AddResult(race, runner);
        AddResult(juniorCourse, runner);

        var gamut = await GetGamutAsync();

        gamut.Runners.Should().BeEmpty();
    }
}
