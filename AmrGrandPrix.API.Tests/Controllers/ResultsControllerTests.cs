using AmrGrandPrix.API.Controllers;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs.RaceResults;
using AmrGrandPrix.API.Services.GrandPrix;
using AmrGrandPrix.API.Services.LlmExtraction;
using AmrGrandPrix.API.Services.ResultsProcessing;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace AmrGrandPrix.API.Tests.Controllers;

public class ResultsControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ResultsController _controller;

    public ResultsControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);

        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var grandPrixService = new GrandPrixCalculationService(
            _context, Mock.Of<ILogger<GrandPrixCalculationService>>());

        _controller = new ResultsController(
            _context,
            Mock.Of<ILlmExtractionService>(),
            new ResultsProcessingService(Mock.Of<ILogger<ResultsProcessingService>>()),
            Mock.Of<IRunnerMatchingService>(),
            grandPrixService,
            userManager.Object,
            Mock.Of<ILogger<ResultsController>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            }
        };
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task SaveResults_ForGrandPrixRace_ShouldAutomaticallyCalculatePointsAndStandings()
    {
        // Arrange
        var race = new Race
        {
            RaceId = Guid.NewGuid(),
            Name = "Test GP Race",
            IsGrandPrixRace = true,
            Date = new DateOnly(2026, 6, 1),
            Year = 2026
        };
        var batch = new UploadBatch
        {
            UploadBatchId = Guid.NewGuid(),
            RaceId = race.RaceId,
            FileName = "test.csv",
            FileType = FileType.CSV,
            Status = UploadStatus.Pending
        };
        _context.Races.Add(race);
        _context.UploadBatches.Add(batch);
        await _context.SaveChangesAsync();

        var request = new SaveResultsRequest
        {
            UploadBatchId = batch.UploadBatchId,
            RaceId = race.RaceId,
            Results = new List<ResultRow>
            {
                new() { Name = "Alice Runner", Age = 30, Gender = Gender.Female, Place = 1,
                        TimeString = "20:00", Status = ResultStatus.Finished },
                new() { Name = "Bob Runner", Age = 35, Gender = Gender.Male, Place = 2,
                        TimeString = "22:00", Status = ResultStatus.Finished }
            }
        };

        // Act
        var result = await _controller.SaveResults(request);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<SaveResultsResponse>().Subject;
        response.Success.Should().BeTrue();
        response.ResultsSaved.Should().Be(2);

        var points = await _context.GrandPrixPoints.Where(p => p.RaceId == race.RaceId).ToListAsync();
        points.Should().NotBeEmpty("saving results for a Grand Prix race should automatically compute GP points");

        var standings = await _context.GrandPrixStandings.Where(s => s.Year == race.Year).ToListAsync();
        standings.Should().NotBeEmpty("saving results for a Grand Prix race should automatically update standings");
    }

    [Fact]
    public async Task SaveResults_ForNonGrandPrixRace_ShouldNotCalculatePointsOrStandings()
    {
        // Arrange
        var race = new Race
        {
            RaceId = Guid.NewGuid(),
            Name = "Fun Run",
            IsGrandPrixRace = false,
            Date = new DateOnly(2026, 6, 1),
            Year = 2026
        };
        var batch = new UploadBatch
        {
            UploadBatchId = Guid.NewGuid(),
            RaceId = race.RaceId,
            FileName = "test.csv",
            FileType = FileType.CSV,
            Status = UploadStatus.Pending
        };
        _context.Races.Add(race);
        _context.UploadBatches.Add(batch);
        await _context.SaveChangesAsync();

        var request = new SaveResultsRequest
        {
            UploadBatchId = batch.UploadBatchId,
            RaceId = race.RaceId,
            Results = new List<ResultRow>
            {
                new() { Name = "Alice Runner", Age = 30, Gender = Gender.Female, Place = 1,
                        TimeString = "20:00", Status = ResultStatus.Finished }
            }
        };

        // Act
        var result = await _controller.SaveResults(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        var points = await _context.GrandPrixPoints.Where(p => p.RaceId == race.RaceId).ToListAsync();
        points.Should().BeEmpty();

        var standings = await _context.GrandPrixStandings.Where(s => s.Year == race.Year).ToListAsync();
        standings.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveResults_NewRunner_EstimatesBirthYearFromRaceDate_NotUploadTime()
    {
        // Arrange — a race that happened years ago; the estimate should be anchored to the race
        // date, not to whatever day the file happens to get uploaded.
        var race = new Race
        {
            RaceId = Guid.NewGuid(),
            Name = "Old Race",
            IsGrandPrixRace = false,
            Date = new DateOnly(2020, 6, 1),
            Year = 2020
        };
        var batch = new UploadBatch
        {
            UploadBatchId = Guid.NewGuid(),
            RaceId = race.RaceId,
            FileName = "test.csv",
            FileType = FileType.CSV,
            Status = UploadStatus.Pending
        };
        _context.Races.Add(race);
        _context.UploadBatches.Add(batch);
        await _context.SaveChangesAsync();

        var request = new SaveResultsRequest
        {
            UploadBatchId = batch.UploadBatchId,
            RaceId = race.RaceId,
            Results = new List<ResultRow>
            {
                new() { Name = "New Runner", Age = 30, Gender = Gender.Female, Place = 1,
                        TimeString = "20:00", Status = ResultStatus.Finished }
            }
        };

        // Act
        await _controller.SaveResults(request);

        // Assert
        var runner = await _context.Runners.SingleAsync(r => r.FirstName == "New" && r.LastName == "Runner");
        runner.EstimatedBirthYear.Should().Be(1990); // 2020 (race year) - 30, not DateTime.UtcNow.Year - 30
        runner.DateOfBirth.Should().BeNull();
    }

    [Fact]
    public async Task SaveResults_UpdateRunnerAge_SetsEstimatedBirthYearFromRaceDate()
    {
        // Arrange
        var runner = new Runner
        {
            RunnerId = Guid.NewGuid(),
            FirstName = "Existing",
            LastName = "Runner",
            Gender = Gender.Male,
            EstimatedBirthYear = 1980
        };
        var race = new Race
        {
            RaceId = Guid.NewGuid(),
            Name = "Race",
            IsGrandPrixRace = false,
            Date = new DateOnly(2020, 6, 1),
            Year = 2020
        };
        var batch = new UploadBatch
        {
            UploadBatchId = Guid.NewGuid(),
            RaceId = race.RaceId,
            FileName = "test.csv",
            FileType = FileType.CSV,
            Status = UploadStatus.Pending
        };
        _context.Runners.Add(runner);
        _context.Races.Add(race);
        _context.UploadBatches.Add(batch);
        await _context.SaveChangesAsync();

        var request = new SaveResultsRequest
        {
            UploadBatchId = batch.UploadBatchId,
            RaceId = race.RaceId,
            Results = new List<ResultRow>
            {
                new() { Name = "Existing Runner", Age = 45, Gender = Gender.Male, Place = 1,
                        TimeString = "20:00", Status = ResultStatus.Finished,
                        MatchedRunnerId = runner.RunnerId, UpdateRunnerAge = true }
            }
        };

        // Act
        await _controller.SaveResults(request);

        // Assert
        var updated = await _context.Runners.SingleAsync(r => r.RunnerId == runner.RunnerId);
        updated.EstimatedBirthYear.Should().Be(1975); // 2020 (race year) - 45
        updated.DateOfBirth.Should().BeNull();
    }

    [Fact]
    public async Task SaveResults_UpdateRunnerAge_NeverOverwritesVerifiedDateOfBirth()
    {
        // Arrange — a runner with a verified profile DOB; the admin's "update stored age"
        // correction during results review must never silently overwrite it.
        var verifiedDob = new DateOnly(1975, 1, 1);
        var runner = new Runner
        {
            RunnerId = Guid.NewGuid(),
            FirstName = "Verified",
            LastName = "Runner",
            Gender = Gender.Male,
            DateOfBirth = verifiedDob
        };
        var race = new Race
        {
            RaceId = Guid.NewGuid(),
            Name = "Race",
            IsGrandPrixRace = false,
            Date = new DateOnly(2020, 6, 1),
            Year = 2020
        };
        var batch = new UploadBatch
        {
            UploadBatchId = Guid.NewGuid(),
            RaceId = race.RaceId,
            FileName = "test.csv",
            FileType = FileType.CSV,
            Status = UploadStatus.Pending
        };
        _context.Runners.Add(runner);
        _context.Races.Add(race);
        _context.UploadBatches.Add(batch);
        await _context.SaveChangesAsync();

        var request = new SaveResultsRequest
        {
            UploadBatchId = batch.UploadBatchId,
            RaceId = race.RaceId,
            Results = new List<ResultRow>
            {
                new() { Name = "Verified Runner", Age = 45, Gender = Gender.Male, Place = 1,
                        TimeString = "20:00", Status = ResultStatus.Finished,
                        MatchedRunnerId = runner.RunnerId, UpdateRunnerAge = true }
            }
        };

        // Act
        await _controller.SaveResults(request);

        // Assert
        var updated = await _context.Runners.SingleAsync(r => r.RunnerId == runner.RunnerId);
        updated.DateOfBirth.Should().Be(verifiedDob);
        updated.EstimatedBirthYear.Should().BeNull();
    }

    [Fact]
    public async Task SaveResults_MissingGender_IsSurfacedInSkippedResults()
    {
        // Arrange
        var race = new Race
        {
            RaceId = Guid.NewGuid(),
            Name = "Race",
            IsGrandPrixRace = false,
            Date = new DateOnly(2026, 6, 1),
            Year = 2026
        };
        var batch = new UploadBatch
        {
            UploadBatchId = Guid.NewGuid(),
            RaceId = race.RaceId,
            FileName = "test.csv",
            FileType = FileType.CSV,
            Status = UploadStatus.Pending
        };
        _context.Races.Add(race);
        _context.UploadBatches.Add(batch);
        await _context.SaveChangesAsync();

        var request = new SaveResultsRequest
        {
            UploadBatchId = batch.UploadBatchId,
            RaceId = race.RaceId,
            Results = new List<ResultRow>
            {
                new() { RowNumber = 1, Name = "No Gender Runner", Age = 30, Gender = null, Place = 1,
                        TimeString = "20:00", Status = ResultStatus.Finished },
                new() { RowNumber = 2, Name = "Alice Runner", Age = 30, Gender = Gender.Female, Place = 2,
                        TimeString = "21:00", Status = ResultStatus.Finished }
            }
        };

        // Act
        var result = await _controller.SaveResults(request);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<SaveResultsResponse>().Subject;
        response.ResultsSaved.Should().Be(1);
        response.SkippedResults.Should().ContainSingle();
        response.SkippedResults[0].RowNumber.Should().Be(1);
        response.SkippedResults[0].Name.Should().Be("No Gender Runner");
        response.SkippedResults[0].Reason.Should().Be("Missing gender");
    }
}
