using System.Security.Claims;
using AmrGrandPrix.API.Controllers;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs;
using AmrGrandPrix.API.Models.DTOs.RaceResults;
using AmrGrandPrix.API.Services.GrandPrix;
using AmrGrandPrix.API.Services.LlmExtraction;
using AmrGrandPrix.API.Services.ResultsProcessing;
using AmrGrandPrix.API.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;

namespace AmrGrandPrix.API.Tests.Controllers;

/// <summary>
/// Finalizing a Grand Prix season locks its standings: no recalculation, and no result changes
/// on its GP races, until it's un-finalized.
/// </summary>
public class GrandPrixSeasonFinalizationTests : IDisposable
{
    private const int Year = 2025;

    private readonly ApplicationDbContext _context;
    private readonly GrandPrixCalculationService _gp;
    private readonly StandingsController _standings;
    private readonly ResultsController _results;
    private readonly RacesController _races;

    private readonly Race _gpRace;
    private readonly Race _nonGpRace;
    private readonly RaceResult _gpResult;

    public GrandPrixSeasonFinalizationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new ApplicationDbContext(options);
        _gp = new GrandPrixCalculationService(_context, Mock.Of<ILogger<GrandPrixCalculationService>>());

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "admin-1")], "Test"))
        };
        _standings = new StandingsController(_context, _gp, Mock.Of<ILogger<StandingsController>>())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var userManager = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        _results = new ResultsController(
            _context,
            Mock.Of<ILlmExtractionService>(),
            new ResultsProcessingService(Mock.Of<ILogger<ResultsProcessingService>>()),
            Mock.Of<IRunnerMatchingService>(),
            _gp,
            userManager.Object,
            Mock.Of<ILogger<ResultsController>>())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
        _races = new RacesController(_context, _gp, Mock.Of<ILogger<RacesController>>());

        var runner = new Runner { RunnerId = Guid.NewGuid(), FirstName = "Fast", LastName = "Runner", Gender = Gender.Male };
        _gpRace = new Race
        {
            RaceId = Guid.NewGuid(), RaceVariant = TestData.Variant("Knoya", "Full Monty", isGrandPrixByDefault: true),
            IsGrandPrixRace = true, Year = Year, Date = new DateOnly(Year, 5, 22)
        };
        _nonGpRace = new Race
        {
            RaceId = Guid.NewGuid(), RaceVariant = TestData.Variant("Crow Pass Crossing"),
            IsGrandPrixRace = false, Year = Year, Date = new DateOnly(Year, 7, 18)
        };
        _gpResult = new RaceResult
        {
            ResultId = Guid.NewGuid(), RaceId = _gpRace.RaceId, RunnerId = runner.RunnerId, Place = 1,
            Time = TimeSpan.FromMinutes(60), Age = 35, AgeCategory = "30-39", Gender = Gender.Male,
            Status = ResultStatus.Finished, UploadBatchId = Guid.NewGuid()
        };
        _context.Runners.Add(runner);
        _context.Races.AddRange(_gpRace, _nonGpRace);
        _context.RaceResults.Add(_gpResult);
        _context.SaveChanges();
    }

    public void Dispose() => _context.Database.EnsureDeleted();

    private async Task FinalizeAsync() =>
        (await _standings.FinalizeSeason(Year)).Result.Should().BeOfType<OkObjectResult>();

    [Fact]
    public async Task FinalizeSeason_RecalculatesStandingsAndLocksTheYear()
    {
        var result = await _standings.FinalizeSeason(Year);

        var season = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeOfType<GrandPrixSeasonDto>().Subject;
        season.IsFinalized.Should().BeTrue();
        season.FinalizedAt.Should().NotBeNull();
        (await _context.GrandPrixSeasons.FindAsync(Year))!.FinalizedBy.Should().Be("admin-1");
        (await _context.GrandPrixStandings.AnyAsync(s => s.Year == Year && s.RunnerId == _gpResult.RunnerId)).Should().BeTrue();
    }

    [Fact]
    public async Task FinalizeSeason_AlreadyFinalized_ReturnsConflict()
    {
        await FinalizeAsync();

        (await _standings.FinalizeSeason(Year)).Result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task GetSeason_ListsGrandPrixRacesWithoutResults()
    {
        _context.Races.Add(new Race
        {
            RaceId = Guid.NewGuid(), RaceVariant = TestData.Variant("Veins of Gold"),
            IsGrandPrixRace = true, Year = Year, Date = new DateOnly(Year, 8, 23)
        });
        await _context.SaveChangesAsync();

        var season = (GrandPrixSeasonDto)((OkObjectResult)(await _standings.GetSeason(Year)).Result!).Value!;

        season.IsFinalized.Should().BeFalse();
        season.GrandPrixRaceCount.Should().Be(2);
        season.GrandPrixRacesWithoutResults.Should().Equal("Veins of Gold");
    }

    [Fact]
    public async Task Recalculate_WhenFinalized_ReturnsConflictAndKeepsStandings()
    {
        await FinalizeAsync();
        var standingsBefore = await _context.GrandPrixStandings.CountAsync(s => s.Year == Year);

        var result = await _standings.RecalculateStandings(Year);

        result.Should().BeOfType<ConflictObjectResult>();
        (await _context.GrandPrixStandings.CountAsync(s => s.Year == Year)).Should().Be(standingsBefore);
    }

    [Fact]
    public async Task UnfinalizeSeason_AllowsRecalculationAgain_ThenCanRefinalize()
    {
        await FinalizeAsync();

        (await _standings.UnfinalizeSeason(Year)).Result.Should().BeOfType<OkObjectResult>();
        (await _standings.RecalculateStandings(Year)).Should().BeOfType<OkObjectResult>();
        (await _standings.FinalizeSeason(Year)).Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UnfinalizeSeason_NotFinalized_ReturnsConflict()
    {
        (await _standings.UnfinalizeSeason(Year)).Result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task SaveResults_ToGrandPrixRace_WhenFinalized_ReturnsConflict()
    {
        await FinalizeAsync();
        var batch = new UploadBatch { UploadBatchId = Guid.NewGuid(), RaceId = _gpRace.RaceId, FileName = "late.pdf", Status = UploadStatus.Pending };
        _context.UploadBatches.Add(batch);
        await _context.SaveChangesAsync();

        var result = await _results.SaveResults(new SaveResultsRequest
        {
            UploadBatchId = batch.UploadBatchId,
            RaceId = _gpRace.RaceId,
            Results = [new() { Name = "Late Entry", Age = 40, Gender = Gender.Male, Place = 2, TimeString = "1:10:00" }]
        });

        result.Result.Should().BeOfType<ConflictObjectResult>();
        (await _context.RaceResults.CountAsync(r => r.RaceId == _gpRace.RaceId)).Should().Be(1);
    }

    [Fact]
    public async Task SaveResults_ToNonGrandPrixRace_WhenFinalized_IsAllowed()
    {
        await FinalizeAsync();
        var batch = new UploadBatch { UploadBatchId = Guid.NewGuid(), RaceId = _nonGpRace.RaceId, FileName = "crow.pdf", Status = UploadStatus.Pending };
        _context.UploadBatches.Add(batch);
        await _context.SaveChangesAsync();

        var result = await _results.SaveResults(new SaveResultsRequest
        {
            UploadBatchId = batch.UploadBatchId,
            RaceId = _nonGpRace.RaceId,
            Results = [new() { Name = "Crow Runner", Age = 40, Gender = Gender.Male, Place = 1, TimeString = "3:10:00" }]
        });

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateAndDeleteResult_OnGrandPrixRace_WhenFinalized_ReturnConflictAndChangeNothing()
    {
        await FinalizeAsync();

        var update = await _results.UpdateRaceResult(_gpResult.ResultId, new UpdateRaceResultRequest
        {
            Place = 5, TimeString = "1:30:00", Age = 35, Gender = Gender.Male, Status = ResultStatus.Finished
        });
        var delete = await _results.DeleteRaceResult(_gpResult.ResultId);

        update.Result.Should().BeOfType<ConflictObjectResult>();
        delete.Should().BeOfType<ConflictObjectResult>();
        var stored = await _context.RaceResults.AsNoTracking().SingleAsync(r => r.ResultId == _gpResult.ResultId);
        stored.Place.Should().Be(1);
    }

    [Fact]
    public async Task UpdateRace_ChangingGrandPrixFlag_WhenFinalized_ReturnsConflict()
    {
        await FinalizeAsync();

        // e.g. trying to move the GP to another Knoya variant after the season was locked
        var result = await _races.UpdateRace(_gpRace.RaceId, new UpdateRaceRequest
        {
            RaceVariantId = _gpRace.RaceVariantId, Date = _gpRace.Date, IsGrandPrixRace = false
        });

        result.Result.Should().BeOfType<ConflictObjectResult>();
        (await _context.Races.AsNoTracking().SingleAsync(r => r.RaceId == _gpRace.RaceId)).IsGrandPrixRace.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateStandingsAsync_WhenFinalized_Throws()
    {
        await FinalizeAsync();

        var act = () => _gp.UpdateStandingsAsync(Year);

        await act.Should().ThrowAsync<GrandPrixSeasonFinalizedException>();
    }
}
