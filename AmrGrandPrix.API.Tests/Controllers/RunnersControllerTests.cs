using AmrGrandPrix.API.Common;
using AmrGrandPrix.API.Controllers;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Tests.Infrastructure;
using AmrGrandPrix.API.Services.ResultsProcessing;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AmrGrandPrix.API.Tests.Controllers;

public class RunnersControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly RunnersController _controller;
    private readonly Guid _batchId = Guid.NewGuid();

    public RunnersControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _controller = new RunnersController(
            _context,
            Mock.Of<IRunnerMatchingService>(),
            Mock.Of<ILogger<RunnersController>>());
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private Runner AddRunner(string first, Gender gender, int? estimatedBirthYear = null)
    {
        var runner = new Runner
        {
            RunnerId = Guid.NewGuid(),
            FirstName = first,
            LastName = "Test",
            Gender = gender,
            EstimatedBirthYear = estimatedBirthYear
        };
        _context.Runners.Add(runner);
        return runner;
    }

    private Race AddRace(int year, RaceSeries? series = null, string? variant = null)
    {
        var raceVariant = series == null
            ? TestData.Variant("Solo Race " + year)
            : series.Variants.FirstOrDefault(v => v.Name == variant) ?? TestData.AddVariant(series, variant!);
        var race = new Race
        {
            RaceId = Guid.NewGuid(),
            RaceVariant = raceVariant,
            Year = year,
            Date = new DateOnly(year, 7, 4)
        };
        _context.Races.Add(race);
        return race;
    }

    private void AddResult(Race race, Runner runner, string? time, string? ageCategory = "30-39",
        ResultStatus status = ResultStatus.Finished, Gender? gender = null)
    {
        _context.RaceResults.Add(new RaceResult
        {
            ResultId = Guid.NewGuid(),
            RaceId = race.RaceId,
            RunnerId = runner.RunnerId,
            Time = time == null ? null : TimeSpan.Parse(time),
            AgeCategory = ageCategory,
            Gender = gender ?? runner.Gender,
            Status = status,
            UploadBatchId = _batchId
        });
    }

    private async Task<RunnerProfileDto> GetProfile(Guid runnerId)
    {
        var result = await _controller.GetRunnerProfile(runnerId);
        return result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<RunnerProfileDto>().Subject;
    }

    [Fact]
    public async Task GetRunnerProfile_UnknownRunner_ReturnsNotFound()
    {
        var result = await _controller.GetRunnerProfile(Guid.NewGuid());

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetRunnerProfile_ComputesPlacesFromTimeAcrossWholeRace()
    {
        var me = AddRunner("Me", Gender.Female, estimatedBirthYear: 1990);
        var fastMan = AddRunner("FastMan", Gender.Male);
        var fastWoman = AddRunner("FastWoman", Gender.Female);
        var olderWoman = AddRunner("OlderWoman", Gender.Female);
        var slowWoman = AddRunner("SlowWoman", Gender.Female);
        var dnf = AddRunner("Dnf", Gender.Female);
        var race = AddRace(2025);

        AddResult(race, fastMan, "00:40:00");
        AddResult(race, fastWoman, "00:45:00");
        AddResult(race, olderWoman, "00:48:00", ageCategory: "40-49");
        AddResult(race, me, "00:50:00");
        AddResult(race, slowWoman, "00:55:00");
        AddResult(race, dnf, null, status: ResultStatus.DNF);
        await _context.SaveChangesAsync();

        var profile = await GetProfile(me.RunnerId);

        profile.CurrentAge.Should().Be(DateTime.Today.Year - 1990);
        profile.IsAgeEstimated.Should().BeTrue();

        var result = profile.Results.Should().ContainSingle().Subject;
        result.OverallPlace.Should().Be(4);
        result.OverallFinishers.Should().Be(5);
        result.GenderPlace.Should().Be(3);
        result.GenderFinishers.Should().Be(4);
        result.AgeGroupPlace.Should().Be(2);
        result.AgeGroupFinishers.Should().Be(3);
        result.PercentBehindWinner.Should().Be(11.1); // 50:00 vs 45:00 female winner
        result.AgeGradedTime.Should().BeNull();        // 35 at race time, below grading age
    }

    [Fact]
    public async Task GetRunnerProfile_AgeGradesResultsFromAge45()
    {
        var me = AddRunner("Me", Gender.Male, estimatedBirthYear: 1970);
        AddResult(AddRace(2014), me, "01:00:00"); // 44 - not graded
        AddResult(AddRace(2020), me, "01:00:00"); // 50
        await _context.SaveChangesAsync();

        var profile = await GetProfile(me.RunnerId);

        profile.Results.Single(r => r.Year == 2014).AgeGradedTime.Should().BeNull();
        profile.Results.Single(r => r.Year == 2020).AgeGradedTime
            .Should().Be(TimeSpan.FromSeconds(Math.Round(3600 * AgeGrading.GetFactor(50, Gender.Male))));
    }

    [Fact]
    public async Task GetRunnerProfile_IncludesGrandPrixHistoryWithDivisionSize()
    {
        var me = AddRunner("Me", Gender.Female);
        var other = AddRunner("Other", Gender.Female);
        _context.GrandPrixStandings.AddRange(
            new GrandPrixStanding { StandingId = Guid.NewGuid(), RunnerId = me.RunnerId, Year = 2024, Division = Division.OpenFemale, Rank = 2, TotalPoints = 340, RacesCompleted = 7, RacesCounted = 4, RunTheGamutQualified = true },
            new GrandPrixStanding { StandingId = Guid.NewGuid(), RunnerId = other.RunnerId, Year = 2024, Division = Division.OpenFemale, Rank = 1, TotalPoints = 380 },
            new GrandPrixStanding { StandingId = Guid.NewGuid(), RunnerId = me.RunnerId, Year = 2024, Division = Division.AgeFemale, AgeCategory = "30-39", Rank = 1, TotalPoints = 20 },
            new GrandPrixStanding { StandingId = Guid.NewGuid(), RunnerId = me.RunnerId, Year = 2023, Division = Division.OpenFemale, Rank = 5, TotalPoints = 200 });
        await _context.SaveChangesAsync();

        var history = (await GetProfile(me.RunnerId)).GrandPrixHistory;

        history.Select(h => (h.Year, h.Division)).Should().Equal(
            (2024, Division.OpenFemale), (2024, Division.AgeFemale), (2023, Division.OpenFemale));
        history[0].DivisionSize.Should().Be(2);
        history[0].RunTheGamutQualified.Should().BeTrue();
        history[1].DivisionSize.Should().Be(1);
    }

    [Fact]
    public async Task GetRunnerProfile_NonFinish_HasNoPlaces()
    {
        var me = AddRunner("Me", Gender.Male);
        var race = AddRace(2025);
        AddResult(race, me, null, status: ResultStatus.DNF);
        await _context.SaveChangesAsync();

        var result = (await GetProfile(me.RunnerId)).Results.Should().ContainSingle().Subject;

        result.OverallPlace.Should().BeNull();
        result.GenderPlace.Should().BeNull();
        result.AgeGroupPlace.Should().BeNull();
        result.IsPersonalRecord.Should().BeFalse();
    }

    [Fact]
    public async Task GetRunnerProfile_FlagsFastestTimePerSeriesVariant()
    {
        var me = AddRunner("Me", Gender.Male);
        var series = new RaceSeries { RaceSeriesId = Guid.NewGuid(), Name = "Mount Marathon" };
        _context.RaceSeries.Add(series);

        AddResult(AddRace(2022, series, "Full"), me, "01:10:00");
        AddResult(AddRace(2023, series, "Full"), me, "01:05:00");
        AddResult(AddRace(2024, series, "Full"), me, "01:08:00");
        AddResult(AddRace(2024, series, "Junior"), me, "00:30:00"); // only finish on this variant
        await _context.SaveChangesAsync();

        var profile = await GetProfile(me.RunnerId);

        profile.Results.Select(r => r.Year).Should().BeInDescendingOrder();
        profile.Results.Where(r => r.IsPersonalRecord).Should().ContainSingle()
            .Which.Time.Should().Be(TimeSpan.Parse("01:05:00"));

        var summary = profile.Series.Should().ContainSingle().Subject;
        summary.Name.Should().Be("Mount Marathon");
        summary.ResultCount.Should().Be(4);
        summary.FirstYear.Should().Be(2022);
        summary.LastYear.Should().Be(2024);
        summary.Variants.Should().HaveCount(2);
        summary.Variants.Single(v => v.CourseVariant == "Full").BestTime.Should().Be(TimeSpan.Parse("01:05:00"));
    }

    [Fact]
    public async Task GetRunnerProfile_SingleCourseSeries_HasNoVariantLabel()
    {
        var me = AddRunner("Me", Gender.Male);
        AddResult(AddRace(2025), me, "00:20:00");
        await _context.SaveChangesAsync();

        var profile = await GetProfile(me.RunnerId);

        profile.Results.Should().ContainSingle().Which.CourseVariant.Should().BeNull();
        profile.Series.Should().ContainSingle().Which.Variants.Should().ContainSingle()
            .Which.CourseVariant.Should().BeNull();
    }

    [Fact]
    public async Task GetRunnerProfile_SameVariantAcrossYears_GroupsUnderOneVariant()
    {
        // Continuity: two years of the same variant are one course, even when another variant
        // of the series was run in between.
        var me = AddRunner("Me", Gender.Male);
        var series = new RaceSeries { RaceSeriesId = Guid.NewGuid(), Name = "Knoya" };
        _context.RaceSeries.Add(series);

        AddResult(AddRace(2024, series, "Full Monty"), me, "01:10:00");
        AddResult(AddRace(2025, series, "Dome"), me, "00:50:00");
        AddResult(AddRace(2026, series, "Full Monty"), me, "01:05:00");
        await _context.SaveChangesAsync();

        var profile = await GetProfile(me.RunnerId);

        var fullMonty = profile.Series.Single().Variants.Single(v => v.CourseVariant == "Full Monty");
        fullMonty.ResultCount.Should().Be(2);
        fullMonty.BestTime.Should().Be(TimeSpan.Parse("01:05:00"));
        profile.Results.Where(r => r.IsPersonalRecord).Select(r => r.Year).Should().Equal(2026);
    }

    [Fact]
    public async Task GetRunnerProfile_GrandPrixHistory_FlagsOnlyFinalizedSeasons()
    {
        var me = AddRunner("Me", Gender.Male);
        foreach (var year in new[] { 2025, 2026 })
        {
            _context.GrandPrixStandings.Add(new GrandPrixStanding
            {
                StandingId = Guid.NewGuid(), RunnerId = me.RunnerId, Year = year,
                Division = Division.OpenMale, Rank = 3, TotalPoints = 250
            });
        }
        _context.GrandPrixSeasons.Add(new GrandPrixSeason { Year = 2025, IsFinalized = true, FinalizedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        var profile = await GetProfile(me.RunnerId);

        profile.GrandPrixHistory.Single(h => h.Year == 2025).IsFinalized.Should().BeTrue();
        profile.GrandPrixHistory.Single(h => h.Year == 2026).IsFinalized.Should().BeFalse();
    }
}
