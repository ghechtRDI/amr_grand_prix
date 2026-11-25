using System.Net;
using System.Text.Json;
using AmrGrandPrix.API.Controllers;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs;
using AmrGrandPrix.API.Services.GrandPrix;
using AmrGrandPrix.API.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace AmrGrandPrix.API.Tests.Controllers;

/// <summary>
/// Site totals on the landing page count only races and runners that have results.
/// </summary>
public class StatsControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly StatsController _controller;

    public StatsControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        _context = new ApplicationDbContext(options);
        _controller = new StatsController(_context, _cache);
    }

    public void Dispose()
    {
        _context.Dispose();
        _cache.Dispose();
    }

    private Race AddRace(RaceVariant variant, int year)
    {
        var race = new Race
        {
            RaceId = Guid.NewGuid(), RaceVariant = variant, Year = year, Date = new DateOnly(year, 7, 4)
        };
        _context.Races.Add(race);
        return race;
    }

    private Runner AddRunner(string first)
    {
        var runner = new Runner { RunnerId = Guid.NewGuid(), FirstName = first, LastName = "Runner", Gender = Gender.Female };
        _context.Runners.Add(runner);
        return runner;
    }

    private void AddResult(Race race, Runner runner) =>
        _context.RaceResults.Add(new RaceResult
        {
            ResultId = Guid.NewGuid(), RaceId = race.RaceId, RunnerId = runner.RunnerId,
            Status = ResultStatus.Finished, Gender = runner.Gender, UploadBatchId = Guid.NewGuid()
        });

    private async Task<SiteSummaryDto> GetSummaryAsync()
    {
        var result = await _controller.GetSummary();
        return (SiteSummaryDto)((OkObjectResult)result.Result!).Value!;
    }

    [Fact]
    public async Task GetSummary_CountsOnlyRacesAndRunnersWithResults()
    {
        var mmr = TestData.Variant("Mount Marathon");
        var bird = TestData.Variant("Bird Ridge");
        var race2019 = AddRace(mmr, 2019);
        var race2024 = AddRace(mmr, 2024);
        var bird2024 = AddRace(bird, 2024);
        AddRace(bird, 2025); // no results yet: not counted, and 2025 isn't a year with results

        var alice = AddRunner("Alice");
        var bea = AddRunner("Bea");
        AddRunner("NoResults");

        AddResult(race2019, alice);
        AddResult(race2024, alice); // Alice ran three races but is one athlete
        AddResult(bird2024, alice);
        AddResult(bird2024, bea);
        await _context.SaveChangesAsync();

        var summary = await GetSummaryAsync();

        summary.RaceCount.Should().Be(3);
        summary.YearCount.Should().Be(2);
        summary.FirstYear.Should().Be(2019);
        summary.LatestYear.Should().Be(2024);
        summary.AthleteCount.Should().Be(2);
    }

    [Fact]
    public async Task GetSummary_EmptyDatabase_ReturnsZeros()
    {
        var summary = await GetSummaryAsync();

        summary.RaceCount.Should().Be(0);
        summary.YearCount.Should().Be(0);
        summary.FirstYear.Should().BeNull();
        summary.LatestYear.Should().BeNull();
        summary.AthleteCount.Should().Be(0);
    }

    [Fact]
    public async Task GetSummary_IsCached()
    {
        var race = AddRace(TestData.Variant("Crow Pass"), 2023);
        AddResult(race, AddRunner("First"));
        await _context.SaveChangesAsync();
        (await GetSummaryAsync()).AthleteCount.Should().Be(1);

        AddResult(race, AddRunner("Second"));
        await _context.SaveChangesAsync();

        (await GetSummaryAsync()).AthleteCount.Should().Be(1);
        _cache.Remove(StatsController.SummaryCacheKey);
        (await GetSummaryAsync()).AthleteCount.Should().Be(2);
    }
}

/// <summary>The summary endpoint is reachable without logging in.</summary>
public class StatsControllerHttpTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;

    public StatsControllerHttpTests(CustomWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync() => await _factory.InitializeDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Summary_IsPublic()
    {
        var response = await _factory.CreateClient().GetAsync("/api/stats/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.TryGetProperty("raceCount", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("athleteCount", out _).Should().BeTrue();
    }
}

/// <summary>The landing page's leader snapshot asks for only the top N of a division.</summary>
public class OpenStandingsTopTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly StandingsController _controller;

    public OpenStandingsTopTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        _context = new ApplicationDbContext(options);
        var gp = new GrandPrixCalculationService(_context, Mock.Of<ILogger<GrandPrixCalculationService>>());
        _controller = new StandingsController(_context, gp, Mock.Of<ILogger<StandingsController>>());
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task Top_ReturnsFirstRanks_AndFullTotal()
    {
        for (var rank = 1; rank <= 8; rank++)
        {
            var runner = new Runner { RunnerId = Guid.NewGuid(), FirstName = "R", LastName = $"{rank}", Gender = Gender.Male };
            _context.Runners.Add(runner);
            _context.GrandPrixStandings.Add(new GrandPrixStanding
            {
                StandingId = Guid.NewGuid(), RunnerId = runner.RunnerId, Year = 2025,
                Division = Division.OpenMale, Rank = rank, TotalPoints = 400 - rank
            });
        }
        await _context.SaveChangesAsync();

        var result = await _controller.GetOpenDivisionStandings(2025, "male", top: 3);
        var response = (StandingsLeaderboardResponse)((OkObjectResult)result.Result!).Value!;

        response.Standings.Select(s => s.Rank).Should().Equal(1, 2, 3);
        response.TotalRunners.Should().Be(8);
    }

    [Fact]
    public async Task Top_MustBePositive()
    {
        var result = await _controller.GetOpenDivisionStandings(2025, "male", top: 0);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }
}
