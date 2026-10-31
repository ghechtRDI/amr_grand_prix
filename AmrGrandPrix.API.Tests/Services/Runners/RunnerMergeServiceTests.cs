using AmrGrandPrix.API.Controllers;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs;
using AmrGrandPrix.API.Services.GrandPrix;
using AmrGrandPrix.API.Services.ResultsProcessing;
using AmrGrandPrix.API.Services.Runners;
using AmrGrandPrix.API.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AmrGrandPrix.API.Tests.Services.Runners;

public class RunnerMergeServiceTests : IDisposable
{
    private const int Year = 2025;

    private readonly ApplicationDbContext _context;
    private readonly GrandPrixCalculationService _gp;
    private readonly RunnerMergeService _service;
    private readonly RunnersController _controller;
    private readonly Guid _batchId = Guid.NewGuid();

    public RunnerMergeServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        _context = new ApplicationDbContext(options);
        _gp = new GrandPrixCalculationService(_context, Mock.Of<ILogger<GrandPrixCalculationService>>());
        var matching = new RunnerMatchingService(_context, Mock.Of<ILogger<RunnerMatchingService>>());
        _service = new RunnerMergeService(_context, matching, _gp, Mock.Of<ILogger<RunnerMergeService>>());
        _controller = new RunnersController(_context, matching, _service, Mock.Of<ILogger<RunnersController>>());
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private Runner AddRunner(string first, string last, Gender gender = Gender.Male, int? estimatedBirthYear = null)
    {
        var runner = new Runner
        {
            RunnerId = Guid.NewGuid(),
            FirstName = first,
            LastName = last,
            Gender = gender,
            EstimatedBirthYear = estimatedBirthYear
        };
        _context.Runners.Add(runner);
        return runner;
    }

    private Race AddGpRace(string series, int year = Year)
    {
        var race = new Race
        {
            RaceId = Guid.NewGuid(),
            RaceVariant = TestData.Variant(series, isGrandPrixByDefault: true),
            IsGrandPrixRace = true,
            Year = year,
            Date = new DateOnly(year, 6, 1)
        };
        _context.Races.Add(race);
        return race;
    }

    private RaceResult AddResult(Race race, Runner runner, int minutes)
    {
        var result = new RaceResult
        {
            ResultId = Guid.NewGuid(),
            RaceId = race.RaceId,
            RunnerId = runner.RunnerId,
            Time = TimeSpan.FromMinutes(minutes),
            Age = 35,
            AgeCategory = "30-39",
            Gender = runner.Gender,
            Status = ResultStatus.Finished,
            UploadBatchId = _batchId
        };
        _context.RaceResults.Add(result);
        return result;
    }

    /// <summary>Scores every GP race and builds the year's standings, as a results save would.</summary>
    private async Task ScoreAsync(params Race[] races)
    {
        await _context.SaveChangesAsync();
        foreach (var race in races)
            await _gp.CalculateRacePointsAsync(race.RaceId);
        await _gp.UpdateStandingsAsync(Year);
    }

    [Fact]
    public async Task Merge_MovesResultsAndCombinesGrandPrixStandings()
    {
        var josh = AddRunner("Josh", "Taylor", estimatedBirthYear: 1990);
        var joshua = AddRunner("Joshua", "Taylor");
        joshua.DateOfBirth = new DateOnly(1990, 3, 14);
        var raceA = AddGpRace("Race A");
        var raceB = AddGpRace("Race B");
        AddResult(raceA, josh, 60);
        AddResult(raceB, joshua, 60);
        await ScoreAsync(raceA, raceB);

        (await _context.GrandPrixStandings.CountAsync(s => s.Division == Division.OpenMale)).Should().Be(2);

        var result = await _service.MergeAsync(josh.RunnerId, joshua.RunnerId);

        result.ResultsMoved.Should().Be(1);
        result.YearsRecalculated.Should().Equal(Year);
        (await _context.Runners.FindAsync(joshua.RunnerId)).Should().BeNull();
        (await _context.RaceResults.CountAsync(r => r.RunnerId == josh.RunnerId)).Should().Be(2);

        var open = await _context.GrandPrixStandings.SingleAsync(s => s.Division == Division.OpenMale);
        open.RunnerId.Should().Be(josh.RunnerId);
        open.TotalPoints.Should().Be(200);
        open.RacesCompleted.Should().Be(2);
        open.Rank.Should().Be(1);
    }

    [Fact]
    public async Task Merge_AddsBothNamesAsAlternatesAndFillsMissingFields()
    {
        var target = AddRunner("Shauna", "Severson");
        target.AlternateNames = new List<string> { "Shauna S" };
        var source = AddRunner("Shawna", "Severson");
        source.DateOfBirth = new DateOnly(1985, 1, 2);
        source.PreferredName = "Shay";
        source.Email = "shauna@example.com";
        await _context.SaveChangesAsync();

        await _service.MergeAsync(target.RunnerId, source.RunnerId);

        var merged = await _context.Runners.SingleAsync();
        merged.FirstName.Should().Be("Shauna");
        merged.AlternateNames.Should().BeEquivalentTo("Shauna S", "Shauna Severson", "Shawna Severson");
        merged.DateOfBirth.Should().Be(new DateOnly(1985, 1, 2));
        merged.PreferredName.Should().Be("Shay");
        merged.Email.Should().Be("shauna@example.com");
    }

    [Fact]
    public async Task Merge_MovesAccountLinkAndClaims()
    {
        var target = AddRunner("Josh", "Taylor");
        var source = AddRunner("Joshua", "Taylor");
        var user = new ApplicationUser { Id = "user-1", UserName = "josh@example.com", Email = "josh@example.com", RunnerId = source.RunnerId };
        _context.Users.Add(user);
        _context.RunnerClaims.Add(new RunnerClaim
        {
            ClaimId = Guid.NewGuid(), ApplicationUserId = user.Id, RunnerId = source.RunnerId, Status = ClaimStatus.Approved
        });
        await _context.SaveChangesAsync();

        (await _service.PreviewMergeAsync(target.RunnerId, source.RunnerId))!.MovesAccountLink.Should().BeTrue();

        await _service.MergeAsync(target.RunnerId, source.RunnerId);

        (await _context.Users.FindAsync("user-1"))!.RunnerId.Should().Be(target.RunnerId);
        (await _context.RunnerClaims.SingleAsync()).RunnerId.Should().Be(target.RunnerId);
    }

    [Fact]
    public async Task Preview_BothInSameRace_IsBlocked()
    {
        var a = AddRunner("Josh", "Taylor");
        var b = AddRunner("Joshua", "Taylor");
        var race = AddGpRace("Race A");
        AddResult(race, a, 60);
        AddResult(race, b, 70);
        await _context.SaveChangesAsync();

        var preview = await _service.PreviewMergeAsync(a.RunnerId, b.RunnerId);

        preview!.CanMerge.Should().BeFalse();
        preview.BlockingIssues.Should().ContainSingle(i => i.Contains("Race A"));
        (await _controller.MergeRunner(a.RunnerId, new MergeRunnersRequest { SourceRunnerId = b.RunnerId }))
            .Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Merge_AffectingFinalizedSeason_ReturnsConflictAndChangesNothing()
    {
        var a = AddRunner("Josh", "Taylor");
        var b = AddRunner("Joshua", "Taylor");
        var race = AddGpRace("Race A");
        AddResult(race, b, 60);
        await ScoreAsync(race);
        _context.GrandPrixSeasons.Add(new GrandPrixSeason { Year = Year, IsFinalized = true });
        await _context.SaveChangesAsync();

        var response = await _controller.MergeRunner(a.RunnerId, new MergeRunnersRequest { SourceRunnerId = b.RunnerId });

        response.Result.Should().BeOfType<ConflictObjectResult>();
        (await _context.Runners.CountAsync()).Should().Be(2);
        (await _context.GrandPrixStandings.AnyAsync(s => s.RunnerId == b.RunnerId)).Should().BeTrue();
    }

    [Fact]
    public async Task Preview_DifferentGenderAndBirthYear_Warns()
    {
        var a = AddRunner("Sam", "Lee", Gender.Male, estimatedBirthYear: 1960);
        var b = AddRunner("Sam", "Lee", Gender.Female, estimatedBirthYear: 1995);
        await _context.SaveChangesAsync();

        var preview = await _service.PreviewMergeAsync(a.RunnerId, b.RunnerId);

        preview!.CanMerge.Should().BeTrue();
        preview.Warnings.Should().HaveCount(2);
    }

    [Fact]
    public async Task FindPotentialDuplicates_FindsCloseNamesAndSkipsUnrelatedOrSameRacePairs()
    {
        var josh = AddRunner("Josh", "Taylor", estimatedBirthYear: 1990);
        var joshua = AddRunner("Joshua", "Taylor", estimatedBirthYear: 1991);
        AddRunner("Shauna", "Severson", Gender.Female);
        AddRunner("Shauna", "Severson", Gender.Female);
        AddRunner("Maria", "Gonzalez", Gender.Female);
        // Same name, but both ran the same race — two different people.
        var bob1 = AddRunner("Bob", "Smith");
        var bob2 = AddRunner("Bob", "Smith");
        var race = AddGpRace("Race A");
        AddResult(race, bob1, 60);
        AddResult(race, bob2, 61);
        AddResult(race, josh, 62);
        await _context.SaveChangesAsync();

        var duplicates = await _service.FindPotentialDuplicatesAsync();

        duplicates.Should().HaveCount(2);
        duplicates[0].RunnerA.FullName.Should().Be("Shauna Severson");
        duplicates[0].Confidence.Should().Be(1.0);
        var taylors = duplicates[1];
        new[] { taylors.RunnerA.RunnerId, taylors.RunnerB.RunnerId }.Should().BeEquivalentTo(new[] { josh.RunnerId, joshua.RunnerId });
        taylors.RunnerA.RunnerId.Should().Be(josh.RunnerId, "the record with more results is listed first");
        taylors.Confidence.Should().BeGreaterThan(0.75);
        taylors.BirthYearMatch.Should().BeTrue();
    }

    [Fact]
    public async Task FindPotentialDuplicates_MatchesOnAlternateNames()
    {
        var married = AddRunner("Emily", "Walsh", Gender.Female);
        married.AlternateNames = new List<string> { "Emily Brennan" };
        AddRunner("Emily", "Brennan", Gender.Female);
        await _context.SaveChangesAsync();

        var duplicates = await _service.FindPotentialDuplicatesAsync();

        duplicates.Should().ContainSingle().Which.NameSimilarity.Should().Be(1.0);
    }

    [Fact]
    public async Task UpdateRunner_SetsDateOfBirthAndNormalizesAlternateNames()
    {
        var runner = AddRunner("Josh", "Taylor");
        await _context.SaveChangesAsync();

        var response = await _controller.UpdateRunner(runner.RunnerId, new UpdateRunnerRequest
        {
            FirstName = "Josh",
            LastName = "Taylor",
            Gender = Gender.Male,
            DateOfBirth = new DateOnly(1990, 3, 14),
            PreferredName = " ",
            AlternateNames = new List<string> { " Joshua  Taylor ", "joshua taylor", "" }
        });

        response.Result.Should().BeOfType<OkObjectResult>();
        var saved = await _context.Runners.FindAsync(runner.RunnerId);
        saved!.DateOfBirth.Should().Be(new DateOnly(1990, 3, 14));
        saved.PreferredName.Should().BeNull();
        saved.AlternateNames.Should().Equal("Joshua Taylor");
    }
}
