using AmrGrandPrix.API.Controllers;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs;
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
    private readonly IGrandPrixCalculationService _grandPrixService;

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

        _grandPrixService = new GrandPrixCalculationService(
            _context, Mock.Of<ILogger<GrandPrixCalculationService>>());

        _controller = new ResultsController(
            _context,
            Mock.Of<ILlmExtractionService>(),
            new ResultsProcessingService(Mock.Of<ILogger<ResultsProcessingService>>()),
            Mock.Of<IRunnerMatchingService>(),
            _grandPrixService,
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

    [Fact]
    public async Task SaveResults_WithSourceUploadBatchId_ClonesNewBatchForDifferentRace()
    {
        // Arrange: a course-variant group split out of the original upload, routed to a
        // different (non-GP) race, saved without a pre-existing UploadBatchId.
        var primaryRace = new Race
        {
            RaceId = Guid.NewGuid(), Name = "Knoya Open", IsGrandPrixRace = true,
            Date = new DateOnly(2026, 6, 1), Year = 2026
        };
        var juniorRace = new Race
        {
            RaceId = Guid.NewGuid(), Name = "Knoya Junior", IsGrandPrixRace = false,
            Date = new DateOnly(2026, 6, 1), Year = 2026
        };
        var sourceBatch = new UploadBatch
        {
            UploadBatchId = Guid.NewGuid(),
            RaceId = primaryRace.RaceId,
            FileName = "knoya.pdf",
            FileType = FileType.PDF,
            Status = UploadStatus.Saved,
            RawLlmJson = "{\"sections\":[]}",
            LlmModel = "claude-test",
            LlmInputTokens = 100,
            LlmOutputTokens = 50
        };
        _context.Races.AddRange(primaryRace, juniorRace);
        _context.UploadBatches.Add(sourceBatch);
        await _context.SaveChangesAsync();

        var request = new SaveResultsRequest
        {
            UploadBatchId = null,
            SourceUploadBatchId = sourceBatch.UploadBatchId,
            RaceId = juniorRace.RaceId,
            Results = new List<ResultRow>
            {
                new() { Name = "Junior Runner", Age = 12, Gender = Gender.Male, Place = 1,
                        TimeString = "10:00", Status = ResultStatus.Finished }
            }
        };

        // Act
        var result = await _controller.SaveResults(request);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<SaveResultsResponse>().Subject;
        response.Success.Should().BeTrue();
        response.ResultsSaved.Should().Be(1);

        var savedResult = await _context.RaceResults.SingleAsync(r => r.RaceId == juniorRace.RaceId);
        var clonedBatch = await _context.UploadBatches.SingleAsync(b => b.UploadBatchId == savedResult.UploadBatchId);

        clonedBatch.UploadBatchId.Should().NotBe(sourceBatch.UploadBatchId);
        clonedBatch.RaceId.Should().Be(juniorRace.RaceId);
        clonedBatch.Status.Should().Be(UploadStatus.Saved);
        clonedBatch.FileName.Should().Be(sourceBatch.FileName);
        clonedBatch.RawLlmJson.Should().Be(sourceBatch.RawLlmJson);
        clonedBatch.LlmModel.Should().Be(sourceBatch.LlmModel);
        clonedBatch.LlmInputTokens.Should().Be(sourceBatch.LlmInputTokens);
        clonedBatch.LlmOutputTokens.Should().Be(sourceBatch.LlmOutputTokens);

        // The junior race isn't GP-eligible, so no points should be created for it
        var points = await _context.GrandPrixPoints.Where(p => p.RaceId == juniorRace.RaceId).ToListAsync();
        points.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveResults_WithNeitherBatchId_ReturnsBadRequest()
    {
        var race = new Race
        {
            RaceId = Guid.NewGuid(), Name = "Race", IsGrandPrixRace = false,
            Date = new DateOnly(2026, 6, 1), Year = 2026
        };
        _context.Races.Add(race);
        await _context.SaveChangesAsync();

        var request = new SaveResultsRequest
        {
            UploadBatchId = null,
            SourceUploadBatchId = null,
            RaceId = race.RaceId,
            Results = new List<ResultRow>()
        };

        var result = await _controller.SaveResults(request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateRaceResult_ShouldUpdateFieldsAndRecalculatePoints()
    {
        // Arrange
        var race = new Race
        {
            RaceId = Guid.NewGuid(), Name = "GP Race", IsGrandPrixRace = true,
            Date = new DateOnly(2026, 6, 1), Year = 2026
        };
        var runner = new Runner { RunnerId = Guid.NewGuid(), FirstName = "Alice", LastName = "Runner", Gender = Gender.Female };
        var raceResult = new RaceResult
        {
            ResultId = Guid.NewGuid(), RaceId = race.RaceId, RunnerId = runner.RunnerId,
            Place = 1, Time = TimeSpan.FromMinutes(30), Age = 30, Gender = Gender.Female,
            Status = ResultStatus.Finished, UploadBatchId = Guid.NewGuid(), UploadedBy = "system"
        };
        _context.Races.Add(race);
        _context.Runners.Add(runner);
        _context.RaceResults.Add(raceResult);
        await _context.SaveChangesAsync();

        var request = new UpdateRaceResultRequest
        {
            Place = 2,
            TimeString = "31:00",
            Age = 31,
            Gender = Gender.Female,
            Status = ResultStatus.Finished,
            Notes = "Corrected time"
        };

        // Act
        var result = await _controller.UpdateRaceResult(raceResult.ResultId, request);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<RaceResultDto>().Subject;
        dto.Place.Should().Be(2);
        dto.Age.Should().Be(31);
        dto.Notes.Should().Be("Corrected time");
        dto.Time.Should().Be(new TimeSpan(0, 31, 0));

        var points = await _context.GrandPrixPoints.Where(p => p.RaceId == race.RaceId).ToListAsync();
        points.Should().NotBeEmpty("editing a result on a Grand Prix race should recalculate GP points");
    }

    [Fact]
    public async Task UpdateRaceResult_ShouldReturnNotFound_ForNonExistentResult()
    {
        var request = new UpdateRaceResultRequest { Gender = Gender.Male, Status = ResultStatus.Finished };

        var result = await _controller.UpdateRaceResult(Guid.NewGuid(), request);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteRaceResult_ShouldRemoveResultAndRecalculatePoints()
    {
        // Arrange
        var race = new Race
        {
            RaceId = Guid.NewGuid(), Name = "GP Race", IsGrandPrixRace = true,
            Date = new DateOnly(2026, 6, 1), Year = 2026
        };
        var runner = new Runner { RunnerId = Guid.NewGuid(), FirstName = "Bob", LastName = "Runner", Gender = Gender.Male };
        var raceResult = new RaceResult
        {
            ResultId = Guid.NewGuid(), RaceId = race.RaceId, RunnerId = runner.RunnerId,
            Place = 1, Time = TimeSpan.FromMinutes(30), Age = 30, Gender = Gender.Male,
            Status = ResultStatus.Finished, UploadBatchId = Guid.NewGuid(), UploadedBy = "system"
        };
        _context.Races.Add(race);
        _context.Runners.Add(runner);
        _context.RaceResults.Add(raceResult);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.DeleteRaceResult(raceResult.ResultId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        (await _context.RaceResults.FindAsync(raceResult.ResultId)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteRaceResult_ShouldReturnNotFound_ForNonExistentResult()
    {
        var result = await _controller.DeleteRaceResult(Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteUploadBatch_ShouldRemoveResultsAndRecalculateStandings()
    {
        // Arrange
        var race = new Race
        {
            RaceId = Guid.NewGuid(), Name = "GP Race", IsGrandPrixRace = true,
            Date = new DateOnly(2026, 6, 1), Year = 2026
        };
        var batch = new UploadBatch
        {
            UploadBatchId = Guid.NewGuid(), RaceId = race.RaceId,
            FileName = "test.csv", FileType = FileType.CSV, Status = UploadStatus.Saved
        };
        var runner = new Runner { RunnerId = Guid.NewGuid(), FirstName = "Alice", LastName = "Runner", Gender = Gender.Female };
        var raceResult = new RaceResult
        {
            ResultId = Guid.NewGuid(), RaceId = race.RaceId, RunnerId = runner.RunnerId,
            Place = 1, Time = TimeSpan.FromMinutes(20), Age = 30, Gender = Gender.Female,
            Status = ResultStatus.Finished, UploadBatchId = batch.UploadBatchId, UploadedBy = "system"
        };
        _context.Races.Add(race);
        _context.UploadBatches.Add(batch);
        _context.Runners.Add(runner);
        _context.RaceResults.Add(raceResult);
        await _context.SaveChangesAsync();

        await _grandPrixService.RecalculateAfterResultsChangeAsync(race.RaceId);

        // Sanity check: points/standings exist before delete
        (await _context.GrandPrixPoints.Where(p => p.RaceId == race.RaceId).ToListAsync()).Should().NotBeEmpty();
        (await _context.GrandPrixStandings.Where(s => s.Year == race.Year).ToListAsync()).Should().NotBeEmpty();

        // Act
        var result = await _controller.DeleteUploadBatch(batch.UploadBatchId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        (await _context.RaceResults.FindAsync(raceResult.ResultId)).Should().BeNull();
        (await _context.UploadBatches.FindAsync(batch.UploadBatchId)).Should().BeNull();
        (await _context.GrandPrixPoints.Where(p => p.RaceId == race.RaceId).ToListAsync())
            .Should().BeEmpty("the runner's result was deleted so they should earn no GP points for this race");
        (await _context.GrandPrixStandings.Where(s => s.Year == race.Year).ToListAsync())
            .Should().BeEmpty("no runner has any points left for the year once the only result is deleted");
    }

    [Fact]
    public async Task DeleteUploadBatch_ShouldRecalculatePointsForResultsRemainingInSameRace()
    {
        // Arrange: two batches feeding the same GP race (e.g. a corrected re-upload)
        var race = new Race
        {
            RaceId = Guid.NewGuid(), Name = "GP Race", IsGrandPrixRace = true,
            Date = new DateOnly(2026, 6, 1), Year = 2026
        };
        var batchToDelete = new UploadBatch
        {
            UploadBatchId = Guid.NewGuid(), RaceId = race.RaceId,
            FileName = "batch1.csv", FileType = FileType.CSV, Status = UploadStatus.Saved
        };
        var batchToKeep = new UploadBatch
        {
            UploadBatchId = Guid.NewGuid(), RaceId = race.RaceId,
            FileName = "batch2.csv", FileType = FileType.CSV, Status = UploadStatus.Saved
        };

        // Fill the top-20 male open division with faster runners from the batch being deleted,
        // so the surviving runner (currently placed outside the scoring top 20) should move into
        // the money once those faster results are gone.
        var deletedBatchResults = Enumerable.Range(1, 20).Select(i =>
        {
            var runner = new Runner { RunnerId = Guid.NewGuid(), FirstName = $"Fast{i}", LastName = "Runner", Gender = Gender.Male };
            _context.Runners.Add(runner);
            return new RaceResult
            {
                ResultId = Guid.NewGuid(), RaceId = race.RaceId, RunnerId = runner.RunnerId,
                Place = i, Time = TimeSpan.FromMinutes(20 + i), Age = 30, Gender = Gender.Male,
                Status = ResultStatus.Finished, UploadBatchId = batchToDelete.UploadBatchId, UploadedBy = "system"
            };
        }).ToList();

        var survivingRunner = new Runner { RunnerId = Guid.NewGuid(), FirstName = "Steady", LastName = "Runner", Gender = Gender.Male };
        var survivingResult = new RaceResult
        {
            ResultId = Guid.NewGuid(), RaceId = race.RaceId, RunnerId = survivingRunner.RunnerId,
            Place = 21, Time = TimeSpan.FromMinutes(45), Age = 30, Gender = Gender.Male,
            Status = ResultStatus.Finished, UploadBatchId = batchToKeep.UploadBatchId, UploadedBy = "system"
        };

        _context.Races.Add(race);
        _context.UploadBatches.AddRange(batchToDelete, batchToKeep);
        _context.Runners.Add(survivingRunner);
        _context.RaceResults.AddRange(deletedBatchResults);
        _context.RaceResults.Add(survivingResult);
        await _context.SaveChangesAsync();

        await _grandPrixService.RecalculateAfterResultsChangeAsync(race.RaceId);

        // Sanity check: surviving runner earns no points while 20 faster runners are ahead
        var pointsBefore = await _context.GrandPrixPoints
            .Where(p => p.RaceId == race.RaceId && p.RunnerId == survivingRunner.RunnerId)
            .ToListAsync();
        pointsBefore.Should().BeEmpty();

        // Act: delete the batch containing the 20 faster runners
        var result = await _controller.DeleteUploadBatch(batchToDelete.UploadBatchId);

        // Assert
        result.Should().BeOfType<NoContentResult>();

        var survivingResultAfter = await _context.RaceResults.FindAsync(survivingResult.ResultId);
        survivingResultAfter!.PlaceGender.Should().Be(1, "gender places should be recomputed for results left in the race");

        var pointsAfter = await _context.GrandPrixPoints
            .Where(p => p.RaceId == race.RaceId && p.RunnerId == survivingRunner.RunnerId)
            .ToListAsync();
        pointsAfter.Should().NotBeEmpty("the surviving runner now places within the scoring top 20 and should earn GP points");

        var standing = await _context.GrandPrixStandings
            .FirstOrDefaultAsync(s => s.Year == race.Year && s.RunnerId == survivingRunner.RunnerId && s.Division == Division.OpenMale);
        standing.Should().NotBeNull("standings should be rebuilt from the recalculated points");
    }

    [Fact]
    public async Task DeleteUploadBatch_ShouldReturnNotFound_ForNonExistentBatch()
    {
        var result = await _controller.DeleteUploadBatch(Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
