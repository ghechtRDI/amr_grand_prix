using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Services.GrandPrix;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AmrGrandPrix.API.Tests.Services.GrandPrix;

public class GrandPrixCalculationServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly GrandPrixCalculationService _service;
    private readonly Mock<ILogger<GrandPrixCalculationService>> _mockLogger;

    public GrandPrixCalculationServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _mockLogger = new Mock<ILogger<GrandPrixCalculationService>>();
        _service = new GrandPrixCalculationService(_context, _mockLogger.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region Open Division Points Tests

    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 90)]
    [InlineData(3, 85)]
    [InlineData(4, 80)]
    [InlineData(5, 75)]
    [InlineData(6, 70)]
    [InlineData(7, 65)]
    [InlineData(8, 60)]
    [InlineData(9, 55)]
    [InlineData(10, 50)]
    [InlineData(11, 45)]
    [InlineData(12, 40)]
    [InlineData(13, 35)]
    [InlineData(14, 30)]
    [InlineData(15, 25)]
    [InlineData(16, 20)]
    [InlineData(17, 15)]
    [InlineData(18, 10)]
    [InlineData(19, 5)]
    [InlineData(20, 1)]
    public void CalculateOpenDivisionPoints_ShouldReturnCorrectPoints_ForEachPlace(int place, int expectedPoints)
    {
        // Act
        var points = _service.CalculateOpenDivisionPoints(place);

        // Assert
        points.Should().Be(expectedPoints);
    }

    [Fact]
    public void CalculateOpenDivisionPoints_ShouldReturnZero_ForPlacesBeyond20()
    {
        // Act
        var points21 = _service.CalculateOpenDivisionPoints(21);
        var points50 = _service.CalculateOpenDivisionPoints(50);
        var points100 = _service.CalculateOpenDivisionPoints(100);

        // Assert
        points21.Should().Be(0);
        points50.Should().Be(0);
        points100.Should().Be(0);
    }

    [Theory]
    [InlineData(1, 110)]  // 100 + 10 bonus
    [InlineData(5, 85)]   // 75 + 10 bonus
    [InlineData(10, 60)]  // 50 + 10 bonus
    [InlineData(20, 11)]  // 1 + 10 bonus
    public void CalculateOpenDivisionPoints_ShouldAddRecordBonus_WhenNewRecord(int place, int expectedPoints)
    {
        // Act
        var points = _service.CalculateOpenDivisionPoints(place, isNewRecord: true);

        // Assert
        points.Should().Be(expectedPoints);
    }

    [Fact]
    public void CalculateOpenDivisionPoints_RecordBonusBeyondTop20_ShouldStillReturnBonus()
    {
        // Act
        var points = _service.CalculateOpenDivisionPoints(25, isNewRecord: true);

        // Assert
        points.Should().Be(10); // 0 base + 10 bonus
    }

    #endregion

    #region Age Division Points Tests

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 4)]
    [InlineData(3, 3)]
    [InlineData(4, 2)]
    [InlineData(5, 1)]
    public void CalculateAgeDivisionPoints_ShouldReturnCorrectPoints_ForEachPlace(int place, int expectedPoints)
    {
        // Act
        var points = _service.CalculateAgeDivisionPoints(place);

        // Assert
        points.Should().Be(expectedPoints);
    }

    [Fact]
    public void CalculateAgeDivisionPoints_ShouldReturnZero_ForPlacesBeyond5()
    {
        // Act
        var points6 = _service.CalculateAgeDivisionPoints(6);
        var points10 = _service.CalculateAgeDivisionPoints(10);
        var points50 = _service.CalculateAgeDivisionPoints(50);

        // Assert
        points6.Should().Be(0);
        points10.Should().Be(0);
        points50.Should().Be(0);
    }

    #endregion

    #region Age Category Determination Tests

    [Theory]
    [InlineData(10, "17 and Under")]
    [InlineData(17, "17 and Under")]
    [InlineData(18, "19-29")]
    [InlineData(19, "19-29")]
    [InlineData(25, "19-29")]
    [InlineData(29, "19-29")]
    [InlineData(30, "30-39")]
    [InlineData(35, "30-39")]
    [InlineData(39, "30-39")]
    [InlineData(40, "40-49")]
    [InlineData(45, "40-49")]
    [InlineData(49, "40-49")]
    [InlineData(50, "50-59")]
    [InlineData(55, "50-59")]
    [InlineData(59, "50-59")]
    [InlineData(60, "60-69")]
    [InlineData(65, "60-69")]
    [InlineData(69, "60-69")]
    [InlineData(70, "70-79")]
    [InlineData(75, "70-79")]
    [InlineData(79, "70-79")]
    [InlineData(80, "80-89")]
    [InlineData(85, "80-89")]
    [InlineData(89, "80-89")]
    [InlineData(90, "80-89")]
    [InlineData(100, "80-89")]
    public void DetermineAgeCategory_ShouldReturnCorrectCategory(int age, string expectedCategory)
    {
        // Act
        var category = _service.DetermineAgeCategory(age);

        // Assert
        category.Should().Be(expectedCategory);
    }

    [Fact]
    public void DetermineAgeCategory_ShouldHandleBoundaryAges()
    {
        // Test exact boundary values
        _service.DetermineAgeCategory(17).Should().Be("17 and Under");
        _service.DetermineAgeCategory(18).Should().Be("19-29");
        _service.DetermineAgeCategory(29).Should().Be("19-29");
        _service.DetermineAgeCategory(30).Should().Be("30-39");
        _service.DetermineAgeCategory(39).Should().Be("30-39");
        _service.DetermineAgeCategory(40).Should().Be("40-49");
    }

    #endregion

    #region Race Points Calculation Tests

    [Fact]
    public async Task CalculateRacePointsAsync_ShouldReturnZero_ForNonExistentRace()
    {
        // Arrange
        var nonExistentRaceId = Guid.NewGuid();

        // Act
        var result = await _service.CalculateRacePointsAsync(nonExistentRaceId);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task CalculateRacePointsAsync_ShouldReturnZero_ForNonGrandPrixRace()
    {
        // Arrange
        var race = new Race
        {
            RaceId = Guid.NewGuid(),
            Name = "Regular Race",
            Date = new DateTime(2024, 6, 1),
            Year = 2024,
            IsGrandPrixRace = false
        };
        await _context.Races.AddAsync(race);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CalculateRacePointsAsync(race.RaceId);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task CalculateRacePointsAsync_ShouldCalculateOpenDivisionPoints_ForTop20()
    {
        // Arrange
        var race = CreateTestRace(2024, isGrandPrix: true);
        var runner1 = CreateTestRunner("John", "Doe", Gender.Male);
        var runner2 = CreateTestRunner("Jane", "Smith", Gender.Female);

        // Male 1st place, Female 1st place
        var result1 = CreateTestResult(race, runner1, 1, TimeSpan.FromMinutes(30), Gender.Male, 35);
        var result2 = CreateTestResult(race, runner2, 1, TimeSpan.FromMinutes(35), Gender.Female, 28);

        await _context.Races.AddAsync(race);
        await _context.Runners.AddRangeAsync(runner1, runner2);
        await _context.RaceResults.AddRangeAsync(result1, result2);
        await _context.SaveChangesAsync();

        // Act
        var pointsCreated = await _service.CalculateRacePointsAsync(race.RaceId);

        // Assert
        pointsCreated.Should().Be(4); // 2 open division + 2 age division

        var points = await _context.GrandPrixPoints.ToListAsync();

        // Check male open division points
        var maleOpenPoints = points.FirstOrDefault(p =>
            p.RunnerId == runner1.RunnerId && p.Division == Division.OpenMale);
        maleOpenPoints.Should().NotBeNull();
        maleOpenPoints!.Points.Should().Be(100);
        maleOpenPoints.AgeCategory.Should().BeNull();

        // Check female open division points
        var femaleOpenPoints = points.FirstOrDefault(p =>
            p.RunnerId == runner2.RunnerId && p.Division == Division.OpenFemale);
        femaleOpenPoints.Should().NotBeNull();
        femaleOpenPoints!.Points.Should().Be(100);
        femaleOpenPoints.AgeCategory.Should().BeNull();

        // Check male age division points
        var maleAgePoints = points.FirstOrDefault(p =>
            p.RunnerId == runner1.RunnerId && p.Division == Division.AgeMale);
        maleAgePoints.Should().NotBeNull();
        maleAgePoints!.Points.Should().Be(5);
        maleAgePoints.AgeCategory.Should().Be("30-39");

        // Check female age division points
        var femaleAgePoints = points.FirstOrDefault(p =>
            p.RunnerId == runner2.RunnerId && p.Division == Division.AgeFemale);
        femaleAgePoints.Should().NotBeNull();
        femaleAgePoints!.Points.Should().Be(5);
        femaleAgePoints.AgeCategory.Should().Be("19-29");
    }

    [Fact]
    public async Task CalculateRacePointsAsync_ShouldOnlyAwardOpenPoints_ToTop20InGender()
    {
        // Arrange
        var race = CreateTestRace(2024, isGrandPrix: true);
        var runners = new List<Runner>();
        var results = new List<RaceResult>();

        // Create 25 male runners
        for (int i = 1; i <= 25; i++)
        {
            var runner = CreateTestRunner($"Male{i}", "Runner", Gender.Male);
            runners.Add(runner);

            var result = CreateTestResult(race, runner, i, TimeSpan.FromMinutes(30 + i), Gender.Male, 30);
            results.Add(result);
        }

        await _context.Races.AddAsync(race);
        await _context.Runners.AddRangeAsync(runners);
        await _context.RaceResults.AddRangeAsync(results);
        await _context.SaveChangesAsync();

        // Act
        await _service.CalculateRacePointsAsync(race.RaceId);

        // Assert
        var openPoints = await _context.GrandPrixPoints
            .Where(p => p.Division == Division.OpenMale)
            .ToListAsync();

        openPoints.Should().HaveCount(20); // Only top 20 get open division points

        // Verify 21st place doesn't get open points
        var runner21Points = openPoints.Where(p => p.RunnerId == runners[20].RunnerId);
        runner21Points.Should().BeEmpty();
    }

    [Fact]
    public async Task CalculateRacePointsAsync_ShouldAwardAgeDivisionPoints_ToTop5PerCategory()
    {
        // Arrange
        var race = CreateTestRace(2024, isGrandPrix: true);
        var runners = new List<Runner>();
        var results = new List<RaceResult>();

        // Create 10 runners in 30-39 age category
        for (int i = 1; i <= 10; i++)
        {
            var runner = CreateTestRunner($"Runner{i}", "Test", Gender.Male);
            runners.Add(runner);

            var result = CreateTestResult(race, runner, i, TimeSpan.FromMinutes(30 + i), Gender.Male, 35);
            results.Add(result);
        }

        await _context.Races.AddAsync(race);
        await _context.Runners.AddRangeAsync(runners);
        await _context.RaceResults.AddRangeAsync(results);
        await _context.SaveChangesAsync();

        // Act
        await _service.CalculateRacePointsAsync(race.RaceId);

        // Assert
        var agePoints = await _context.GrandPrixPoints
            .Where(p => p.Division == Division.AgeMale && p.AgeCategory == "30-39")
            .ToListAsync();

        agePoints.Should().HaveCount(5); // Only top 5 in age category get points

        // Verify points awarded correctly
        var firstPlacePoints = agePoints.First(p => p.RunnerId == runners[0].RunnerId);
        firstPlacePoints.Points.Should().Be(5);

        var fifthPlacePoints = agePoints.First(p => p.RunnerId == runners[4].RunnerId);
        fifthPlacePoints.Points.Should().Be(1);

        // Verify 6th place in age category doesn't get points
        var runner6Points = agePoints.Where(p => p.RunnerId == runners[5].RunnerId);
        runner6Points.Should().BeEmpty();
    }

    [Fact]
    public async Task CalculateRacePointsAsync_ShouldHandleMultipleAgeCategories()
    {
        // Arrange
        var race = CreateTestRace(2024, isGrandPrix: true);

        var youngRunner = CreateTestRunner("Young", "Runner", Gender.Male);
        var middleRunner = CreateTestRunner("Middle", "Runner", Gender.Male);
        var oldRunner = CreateTestRunner("Old", "Runner", Gender.Male);

        var result1 = CreateTestResult(race, youngRunner, 1, TimeSpan.FromMinutes(30), Gender.Male, 25);
        var result2 = CreateTestResult(race, middleRunner, 2, TimeSpan.FromMinutes(31), Gender.Male, 45);
        var result3 = CreateTestResult(race, oldRunner, 3, TimeSpan.FromMinutes(32), Gender.Male, 65);

        await _context.Races.AddAsync(race);
        await _context.Runners.AddRangeAsync(youngRunner, middleRunner, oldRunner);
        await _context.RaceResults.AddRangeAsync(result1, result2, result3);
        await _context.SaveChangesAsync();

        // Act
        await _service.CalculateRacePointsAsync(race.RaceId);

        // Assert
        var agePoints = await _context.GrandPrixPoints
            .Where(p => p.Division == Division.AgeMale)
            .ToListAsync();

        agePoints.Should().HaveCount(3);

        // Each runner should be 1st in their age category
        agePoints.Where(p => p.AgeCategory == "19-29").Should().ContainSingle()
            .Which.Points.Should().Be(5);
        agePoints.Where(p => p.AgeCategory == "40-49").Should().ContainSingle()
            .Which.Points.Should().Be(5);
        agePoints.Where(p => p.AgeCategory == "60-69").Should().ContainSingle()
            .Which.Points.Should().Be(5);
    }

    [Fact]
    public async Task CalculateRacePointsAsync_ShouldDeleteExistingPoints_BeforeRecalculating()
    {
        // Arrange
        var race = CreateTestRace(2024, isGrandPrix: true);
        var runner = CreateTestRunner("Test", "Runner", Gender.Male);
        var result = CreateTestResult(race, runner, 1, TimeSpan.FromMinutes(30), Gender.Male, 35);

        await _context.Races.AddAsync(race);
        await _context.Runners.AddAsync(runner);
        await _context.RaceResults.AddAsync(result);
        await _context.SaveChangesAsync();

        // First calculation
        await _service.CalculateRacePointsAsync(race.RaceId);
        var firstCount = await _context.GrandPrixPoints.CountAsync();

        // Act - Second calculation (should delete and recreate)
        await _service.CalculateRacePointsAsync(race.RaceId);
        var secondCount = await _context.GrandPrixPoints.CountAsync();

        // Assert
        firstCount.Should().Be(secondCount);
        secondCount.Should().Be(2); // Open + Age division points
    }

    [Fact]
    public async Task CalculateRacePointsAsync_ShouldUpdatePlaceGenderOnResults()
    {
        // Arrange
        var race = CreateTestRace(2024, isGrandPrix: true);
        var runner1 = CreateTestRunner("First", "Runner", Gender.Male);
        var runner2 = CreateTestRunner("Second", "Runner", Gender.Male);

        var result1 = CreateTestResult(race, runner1, 0, TimeSpan.FromMinutes(30), Gender.Male, 35);
        var result2 = CreateTestResult(race, runner2, 0, TimeSpan.FromMinutes(31), Gender.Male, 35);

        await _context.Races.AddAsync(race);
        await _context.Runners.AddRangeAsync(runner1, runner2);
        await _context.RaceResults.AddRangeAsync(result1, result2);
        await _context.SaveChangesAsync();

        // Act
        await _service.CalculateRacePointsAsync(race.RaceId);

        // Assert
        var updatedResults = await _context.RaceResults
            .Where(r => r.RaceId == race.RaceId)
            .OrderBy(r => r.Time)
            .ToListAsync();

        updatedResults[0].PlaceGender.Should().Be(1);
        updatedResults[1].PlaceGender.Should().Be(2);
    }

    [Fact]
    public async Task CalculateRacePointsAsync_ShouldUpdatePlaceAgeCategoryOnResults()
    {
        // Arrange
        var race = CreateTestRace(2024, isGrandPrix: true);
        var runner1 = CreateTestRunner("First", "Runner", Gender.Male);
        var runner2 = CreateTestRunner("Second", "Runner", Gender.Male);

        // Both in same age category (30-39)
        var result1 = CreateTestResult(race, runner1, 0, TimeSpan.FromMinutes(30), Gender.Male, 35);
        var result2 = CreateTestResult(race, runner2, 0, TimeSpan.FromMinutes(31), Gender.Male, 36);

        await _context.Races.AddAsync(race);
        await _context.Runners.AddRangeAsync(runner1, runner2);
        await _context.RaceResults.AddRangeAsync(result1, result2);
        await _context.SaveChangesAsync();

        // Act
        await _service.CalculateRacePointsAsync(race.RaceId);

        // Assert
        var updatedResults = await _context.RaceResults
            .Where(r => r.RaceId == race.RaceId)
            .OrderBy(r => r.Time)
            .ToListAsync();

        updatedResults[0].PlaceAgeCategory.Should().Be(1);
        updatedResults[1].PlaceAgeCategory.Should().Be(2);
    }

    [Fact]
    public async Task CalculateRacePointsAsync_ShouldIgnoreDNFResults()
    {
        // Arrange
        var race = CreateTestRace(2024, isGrandPrix: true);
        var runner1 = CreateTestRunner("Finisher", "Runner", Gender.Male);
        var runner2 = CreateTestRunner("DNF", "Runner", Gender.Male);

        var result1 = CreateTestResult(race, runner1, 1, TimeSpan.FromMinutes(30), Gender.Male, 35);
        var result2 = new RaceResult
        {
            ResultId = Guid.NewGuid(),
            RaceId = race.RaceId,
            Race = race,
            RunnerId = runner2.RunnerId,
            Runner = runner2,
            Status = ResultStatus.DNF,
            Time = null,
            Gender = Gender.Male,
            Age = 35
        };

        await _context.Races.AddAsync(race);
        await _context.Runners.AddRangeAsync(runner1, runner2);
        await _context.RaceResults.AddRangeAsync(result1, result2);
        await _context.SaveChangesAsync();

        // Act
        await _service.CalculateRacePointsAsync(race.RaceId);

        // Assert
        var points = await _context.GrandPrixPoints.ToListAsync();

        // Only finisher should have points
        points.Should().OnlyContain(p => p.RunnerId == runner1.RunnerId);
        points.Should().HaveCount(2); // Open + Age
    }

    #endregion

    #region Standings Calculation Tests

    [Fact]
    public async Task UpdateStandingsAsync_ShouldCalculateStandingsForAllDivisions()
    {
        // Arrange
        var year = 2024;
        var race = CreateTestRace(year, isGrandPrix: true);
        var maleRunner = CreateTestRunner("Male", "Runner", Gender.Male);
        var femaleRunner = CreateTestRunner("Female", "Runner", Gender.Female);

        var result1 = CreateTestResult(race, maleRunner, 1, TimeSpan.FromMinutes(30), Gender.Male, 35);
        var result2 = CreateTestResult(race, femaleRunner, 1, TimeSpan.FromMinutes(35), Gender.Female, 28);

        await _context.Races.AddAsync(race);
        await _context.Runners.AddRangeAsync(maleRunner, femaleRunner);
        await _context.RaceResults.AddRangeAsync(result1, result2);
        await _context.SaveChangesAsync();

        await _service.CalculateRacePointsAsync(race.RaceId);

        // Act
        var standingsCount = await _service.UpdateStandingsAsync(year);

        // Assert
        standingsCount.Should().BeGreaterThan(0);

        var standings = await _context.GrandPrixStandings.ToListAsync();

        // Should have standings for male open, female open, male age, female age
        standings.Should().Contain(s => s.Division == Division.OpenMale);
        standings.Should().Contain(s => s.Division == Division.OpenFemale);
        standings.Should().Contain(s => s.Division == Division.AgeMale);
        standings.Should().Contain(s => s.Division == Division.AgeFemale);
    }

    [Fact]
    public async Task UpdateStandingsAsync_ShouldSelectBest4Races()
    {
        // Arrange
        var year = 2024;
        var runner = CreateTestRunner("Test", "Runner", Gender.Male);
        await _context.Runners.AddAsync(runner);
        await _context.SaveChangesAsync();

        // Create 6 races with different points
        var pointsValues = new[] { 100, 90, 85, 80, 75, 70 };
        for (int i = 0; i < 6; i++)
        {
            var race = new Race
            {
                RaceId = Guid.NewGuid(),
                Name = $"Race {i + 1}",
                Date = new DateTime(year, i + 1, 1),
                Year = year,
                IsGrandPrixRace = true
            };
            await _context.Races.AddAsync(race);

            var points = new GrandPrixPoints
            {
                PointsId = Guid.NewGuid(),
                RunnerId = runner.RunnerId,
                RaceId = race.RaceId,
                Year = year,
                Division = Division.OpenMale,
                Points = pointsValues[i],
                CreatedAt = DateTime.UtcNow
            };
            await _context.GrandPrixPoints.AddAsync(points);
        }
        await _context.SaveChangesAsync();

        // Act
        await _service.UpdateStandingsAsync(year);

        // Assert
        var standing = await _context.GrandPrixStandings
            .FirstAsync(s => s.RunnerId == runner.RunnerId && s.Division == Division.OpenMale);

        standing.RacesCompleted.Should().Be(6);
        standing.RacesCounted.Should().Be(4);
        standing.TotalPoints.Should().Be(355); // 100 + 90 + 85 + 80
        standing.BestRacePoints.Should().Be(100);
        standing.SecondBestRacePoints.Should().Be(90);
        standing.ThirdBestRacePoints.Should().Be(85);
        standing.FourthBestRacePoints.Should().Be(80);
    }

    [Fact]
    public async Task UpdateStandingsAsync_ShouldHandleFewerThan4Races()
    {
        // Arrange
        var year = 2024;
        var runner = CreateTestRunner("Test", "Runner", Gender.Male);
        await _context.Runners.AddAsync(runner);
        await _context.SaveChangesAsync();

        // Create only 2 races
        var pointsValues = new[] { 100, 90 };
        for (int i = 0; i < 2; i++)
        {
            var race = new Race
            {
                RaceId = Guid.NewGuid(),
                Name = $"Race {i + 1}",
                Date = new DateTime(year, i + 1, 1),
                Year = year,
                IsGrandPrixRace = true
            };
            await _context.Races.AddAsync(race);

            var points = new GrandPrixPoints
            {
                PointsId = Guid.NewGuid(),
                RunnerId = runner.RunnerId,
                RaceId = race.RaceId,
                Year = year,
                Division = Division.OpenMale,
                Points = pointsValues[i],
                CreatedAt = DateTime.UtcNow
            };
            await _context.GrandPrixPoints.AddAsync(points);
        }
        await _context.SaveChangesAsync();

        // Act
        await _service.UpdateStandingsAsync(year);

        // Assert
        var standing = await _context.GrandPrixStandings
            .FirstAsync(s => s.RunnerId == runner.RunnerId && s.Division == Division.OpenMale);

        standing.RacesCompleted.Should().Be(2);
        standing.RacesCounted.Should().Be(2);
        standing.TotalPoints.Should().Be(190); // 100 + 90
        standing.BestRacePoints.Should().Be(100);
        standing.SecondBestRacePoints.Should().Be(90);
        standing.ThirdBestRacePoints.Should().Be(0);
        standing.FourthBestRacePoints.Should().Be(0);
    }

    [Fact]
    public async Task UpdateStandingsAsync_ShouldSetRunTheGamutQualified_For7OrMoreRaces()
    {
        // Arrange
        var year = 2024;
        var runner7Races = CreateTestRunner("Seven", "Races", Gender.Male);
        var runner6Races = CreateTestRunner("Six", "Races", Gender.Male);
        await _context.Runners.AddRangeAsync(runner7Races, runner6Races);
        await _context.SaveChangesAsync();

        // Create 7 races for first runner
        for (int i = 0; i < 7; i++)
        {
            var race = new Race
            {
                RaceId = Guid.NewGuid(),
                Name = $"Race {i + 1}",
                Date = new DateTime(year, i + 1, 1),
                Year = year,
                IsGrandPrixRace = true
            };
            await _context.Races.AddAsync(race);

            await _context.GrandPrixPoints.AddAsync(new GrandPrixPoints
            {
                PointsId = Guid.NewGuid(),
                RunnerId = runner7Races.RunnerId,
                RaceId = race.RaceId,
                Year = year,
                Division = Division.OpenMale,
                Points = 50,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Create 6 races for second runner
        for (int i = 0; i < 6; i++)
        {
            var race = new Race
            {
                RaceId = Guid.NewGuid(),
                Name = $"Race B{i + 1}",
                Date = new DateTime(year, i + 1, 15),
                Year = year,
                IsGrandPrixRace = true
            };
            await _context.Races.AddAsync(race);

            await _context.GrandPrixPoints.AddAsync(new GrandPrixPoints
            {
                PointsId = Guid.NewGuid(),
                RunnerId = runner6Races.RunnerId,
                RaceId = race.RaceId,
                Year = year,
                Division = Division.OpenMale,
                Points = 50,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _context.SaveChangesAsync();

        // Act
        await _service.UpdateStandingsAsync(year);

        // Assert
        var standing7 = await _context.GrandPrixStandings
            .FirstAsync(s => s.RunnerId == runner7Races.RunnerId);
        var standing6 = await _context.GrandPrixStandings
            .FirstAsync(s => s.RunnerId == runner6Races.RunnerId);

        standing7.RunTheGamutQualified.Should().BeTrue();
        standing6.RunTheGamutQualified.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateStandingsAsync_ShouldRankByTotalPoints()
    {
        // Arrange
        var year = 2024;
        var runner1 = CreateTestRunner("First", "Place", Gender.Male);
        var runner2 = CreateTestRunner("Second", "Place", Gender.Male);
        var runner3 = CreateTestRunner("Third", "Place", Gender.Male);
        await _context.Runners.AddRangeAsync(runner1, runner2, runner3);
        await _context.SaveChangesAsync();

        var race = new Race
        {
            RaceId = Guid.NewGuid(),
            Name = "Test Race",
            Date = new DateTime(year, 1, 1),
            Year = year,
            IsGrandPrixRace = true
        };
        await _context.Races.AddAsync(race);

        // Give different total points
        await _context.GrandPrixPoints.AddRangeAsync(
            new GrandPrixPoints { PointsId = Guid.NewGuid(), RunnerId = runner1.RunnerId, RaceId = race.RaceId, Year = year, Division = Division.OpenMale, Points = 100, CreatedAt = DateTime.UtcNow },
            new GrandPrixPoints { PointsId = Guid.NewGuid(), RunnerId = runner2.RunnerId, RaceId = race.RaceId, Year = year, Division = Division.OpenMale, Points = 90, CreatedAt = DateTime.UtcNow },
            new GrandPrixPoints { PointsId = Guid.NewGuid(), RunnerId = runner3.RunnerId, RaceId = race.RaceId, Year = year, Division = Division.OpenMale, Points = 85, CreatedAt = DateTime.UtcNow }
        );
        await _context.SaveChangesAsync();

        // Act
        await _service.UpdateStandingsAsync(year);

        // Assert
        var standings = await _context.GrandPrixStandings
            .Where(s => s.Division == Division.OpenMale)
            .OrderBy(s => s.Rank)
            .ToListAsync();

        standings[0].RunnerId.Should().Be(runner1.RunnerId);
        standings[0].Rank.Should().Be(1);
        standings[1].RunnerId.Should().Be(runner2.RunnerId);
        standings[1].Rank.Should().Be(2);
        standings[2].RunnerId.Should().Be(runner3.RunnerId);
        standings[2].Rank.Should().Be(3);
    }

    [Fact]
    public async Task UpdateStandingsAsync_ShouldUseTiebreaker_WhenTotalPointsEqual()
    {
        // Arrange
        var year = 2024;
        var runner1 = CreateTestRunner("Better", "Best", Gender.Male);
        var runner2 = CreateTestRunner("Worse", "Best", Gender.Male);
        await _context.Runners.AddRangeAsync(runner1, runner2);
        await _context.SaveChangesAsync();

        // Both get 190 total points, but different race distributions
        var race1 = CreateRaceWithPoints(year, 1);
        var race2 = CreateRaceWithPoints(year, 2);
        var race3 = CreateRaceWithPoints(year, 3);

        await _context.Races.AddRangeAsync(race1, race2, race3);

        // Runner1: 100 + 90 = 190 (best race: 100)
        await _context.GrandPrixPoints.AddRangeAsync(
            new GrandPrixPoints { PointsId = Guid.NewGuid(), RunnerId = runner1.RunnerId, RaceId = race1.RaceId, Year = year, Division = Division.OpenMale, Points = 100, CreatedAt = DateTime.UtcNow },
            new GrandPrixPoints { PointsId = Guid.NewGuid(), RunnerId = runner1.RunnerId, RaceId = race2.RaceId, Year = year, Division = Division.OpenMale, Points = 90, CreatedAt = DateTime.UtcNow }
        );

        // Runner2: 95 + 95 = 190 (best race: 95)
        await _context.GrandPrixPoints.AddRangeAsync(
            new GrandPrixPoints { PointsId = Guid.NewGuid(), RunnerId = runner2.RunnerId, RaceId = race1.RaceId, Year = year, Division = Division.OpenMale, Points = 95, CreatedAt = DateTime.UtcNow },
            new GrandPrixPoints { PointsId = Guid.NewGuid(), RunnerId = runner2.RunnerId, RaceId = race2.RaceId, Year = year, Division = Division.OpenMale, Points = 95, CreatedAt = DateTime.UtcNow }
        );

        await _context.SaveChangesAsync();

        // Act
        await _service.UpdateStandingsAsync(year);

        // Assert
        var standings = await _context.GrandPrixStandings
            .Where(s => s.Division == Division.OpenMale)
            .OrderBy(s => s.Rank)
            .ToListAsync();

        // Runner1 should be ranked higher due to better best race
        standings[0].RunnerId.Should().Be(runner1.RunnerId);
        standings[0].Rank.Should().Be(1);
        standings[0].BestRacePoints.Should().Be(100);

        standings[1].RunnerId.Should().Be(runner2.RunnerId);
        standings[1].Rank.Should().Be(2);
        standings[1].BestRacePoints.Should().Be(95);
    }

    [Fact]
    public async Task UpdateStandingsAsync_ShouldDeleteExistingStandings_BeforeRecalculating()
    {
        // Arrange
        var year = 2024;
        var runner = CreateTestRunner("Test", "Runner", Gender.Male);
        await _context.Runners.AddAsync(runner);
        await _context.SaveChangesAsync();

        var race = CreateRaceWithPoints(year, 1);
        await _context.Races.AddAsync(race);
        await _context.GrandPrixPoints.AddAsync(new GrandPrixPoints
        {
            PointsId = Guid.NewGuid(),
            RunnerId = runner.RunnerId,
            RaceId = race.RaceId,
            Year = year,
            Division = Division.OpenMale,
            Points = 100,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // First calculation
        await _service.UpdateStandingsAsync(year);
        var firstCount = await _context.GrandPrixStandings.CountAsync();

        // Act - Second calculation
        await _service.UpdateStandingsAsync(year);
        var secondCount = await _context.GrandPrixStandings.CountAsync();

        // Assert
        firstCount.Should().Be(secondCount);
    }

    #endregion

    #region Get Standings Tests

    [Fact]
    public async Task GetStandingsAsync_ShouldReturnStandings_OrderedByRank()
    {
        // Arrange
        var year = 2024;
        var runner1 = CreateTestRunner("First", "Place", Gender.Male);
        var runner2 = CreateTestRunner("Second", "Place", Gender.Male);
        await _context.Runners.AddRangeAsync(runner1, runner2);

        var standing1 = new GrandPrixStanding
        {
            StandingId = Guid.NewGuid(),
            RunnerId = runner1.RunnerId,
            Runner = runner1,
            Year = year,
            Division = Division.OpenMale,
            TotalPoints = 100,
            RacesCompleted = 1,
            RacesCounted = 1,
            Rank = 1
        };

        var standing2 = new GrandPrixStanding
        {
            StandingId = Guid.NewGuid(),
            RunnerId = runner2.RunnerId,
            Runner = runner2,
            Year = year,
            Division = Division.OpenMale,
            TotalPoints = 90,
            RacesCompleted = 1,
            RacesCounted = 1,
            Rank = 2
        };

        await _context.GrandPrixStandings.AddRangeAsync(standing1, standing2);
        await _context.SaveChangesAsync();

        // Act
        var standings = await _service.GetStandingsAsync(year, Division.OpenMale);

        // Assert
        standings.Should().HaveCount(2);
        standings[0].Rank.Should().Be(1);
        standings[0].RunnerId.Should().Be(runner1.RunnerId);
        standings[1].Rank.Should().Be(2);
        standings[1].RunnerId.Should().Be(runner2.RunnerId);
    }

    [Fact]
    public async Task GetStandingsAsync_ShouldFilterByAgeCategory()
    {
        // Arrange
        var year = 2024;
        var runner1 = CreateTestRunner("Young", "Runner", Gender.Male);
        var runner2 = CreateTestRunner("Old", "Runner", Gender.Male);
        await _context.Runners.AddRangeAsync(runner1, runner2);

        await _context.GrandPrixStandings.AddRangeAsync(
            new GrandPrixStanding
            {
                StandingId = Guid.NewGuid(),
                RunnerId = runner1.RunnerId,
                Runner = runner1,
                Year = year,
                Division = Division.AgeMale,
                AgeCategory = "30-39",
                TotalPoints = 5,
                RacesCompleted = 1,
                RacesCounted = 1,
                Rank = 1
            },
            new GrandPrixStanding
            {
                StandingId = Guid.NewGuid(),
                RunnerId = runner2.RunnerId,
                Runner = runner2,
                Year = year,
                Division = Division.AgeMale,
                AgeCategory = "40-49",
                TotalPoints = 5,
                RacesCompleted = 1,
                RacesCounted = 1,
                Rank = 1
            }
        );
        await _context.SaveChangesAsync();

        // Act
        var standings = await _service.GetStandingsAsync(year, Division.AgeMale, "30-39");

        // Assert
        standings.Should().ContainSingle();
        standings[0].AgeCategory.Should().Be("30-39");
        standings[0].RunnerId.Should().Be(runner1.RunnerId);
    }

    #endregion

    #region Get Runner Points Tests

    [Fact]
    public async Task GetRunnerPointsAsync_ShouldReturnPoints_OrderedByRaceDate()
    {
        // Arrange
        var year = 2024;
        var runner = CreateTestRunner("Test", "Runner", Gender.Male);
        await _context.Runners.AddAsync(runner);
        await _context.SaveChangesAsync();

        var race1 = new Race { RaceId = Guid.NewGuid(), Name = "Race 1", Date = new DateTime(year, 1, 1), Year = year, IsGrandPrixRace = true };
        var race2 = new Race { RaceId = Guid.NewGuid(), Name = "Race 2", Date = new DateTime(year, 2, 1), Year = year, IsGrandPrixRace = true };
        await _context.Races.AddRangeAsync(race1, race2);

        await _context.GrandPrixPoints.AddRangeAsync(
            new GrandPrixPoints
            {
                PointsId = Guid.NewGuid(),
                RunnerId = runner.RunnerId,
                RaceId = race2.RaceId,
                Race = race2,
                Year = year,
                Division = Division.OpenMale,
                Points = 90,
                CreatedAt = DateTime.UtcNow
            },
            new GrandPrixPoints
            {
                PointsId = Guid.NewGuid(),
                RunnerId = runner.RunnerId,
                RaceId = race1.RaceId,
                Race = race1,
                Year = year,
                Division = Division.OpenMale,
                Points = 100,
                CreatedAt = DateTime.UtcNow
            }
        );
        await _context.SaveChangesAsync();

        // Act
        var points = await _service.GetRunnerPointsAsync(runner.RunnerId, year);

        // Assert
        points.Should().HaveCount(2);
        points[0].RaceId.Should().Be(race1.RaceId); // Earlier date first
        points[1].RaceId.Should().Be(race2.RaceId);
    }

    [Fact]
    public async Task GetRunnerPointsAsync_ShouldReturnEmpty_ForRunnerWithNoPoints()
    {
        // Arrange
        var runner = CreateTestRunner("Test", "Runner", Gender.Male);
        await _context.Runners.AddAsync(runner);
        await _context.SaveChangesAsync();

        // Act
        var points = await _service.GetRunnerPointsAsync(runner.RunnerId, 2024);

        // Assert
        points.Should().BeEmpty();
    }

    #endregion

    #region Recalculation Tests

    [Fact]
    public async Task RecalculateAfterResultsChangeAsync_ShouldRecalculatePointsAndStandings()
    {
        // Arrange
        var race = CreateTestRace(2024, isGrandPrix: true);
        var runner = CreateTestRunner("Test", "Runner", Gender.Male);
        var result = CreateTestResult(race, runner, 1, TimeSpan.FromMinutes(30), Gender.Male, 35);

        await _context.Races.AddAsync(race);
        await _context.Runners.AddAsync(runner);
        await _context.RaceResults.AddAsync(result);
        await _context.SaveChangesAsync();

        // Act
        var success = await _service.RecalculateAfterResultsChangeAsync(race.RaceId);

        // Assert
        success.Should().BeTrue();

        var points = await _context.GrandPrixPoints.ToListAsync();
        points.Should().NotBeEmpty();

        var standings = await _context.GrandPrixStandings.ToListAsync();
        standings.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RecalculateAfterResultsChangeAsync_ShouldReturnFalse_ForNonExistentRace()
    {
        // Arrange
        var nonExistentRaceId = Guid.NewGuid();

        // Act
        var success = await _service.RecalculateAfterResultsChangeAsync(nonExistentRaceId);

        // Assert
        success.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private Race CreateTestRace(int year, bool isGrandPrix)
    {
        return new Race
        {
            RaceId = Guid.NewGuid(),
            Name = "Test Race",
            Date = new DateTime(year, 6, 1),
            Year = year,
            IsGrandPrixRace = isGrandPrix,
            Results = new List<RaceResult>()
        };
    }

    private Runner CreateTestRunner(string firstName, string lastName, Gender gender)
    {
        return new Runner
        {
            RunnerId = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            Gender = gender
        };
    }

    private RaceResult CreateTestResult(Race race, Runner runner, int place, TimeSpan time, Gender gender, int age)
    {
        return new RaceResult
        {
            ResultId = Guid.NewGuid(),
            RaceId = race.RaceId,
            Race = race,
            RunnerId = runner.RunnerId,
            Runner = runner,
            Place = place > 0 ? place : null,
            Time = time,
            Status = ResultStatus.Finished,
            Gender = gender,
            Age = age,
            UploadBatchId = Guid.NewGuid()
        };
    }

    private Race CreateRaceWithPoints(int year, int month)
    {
        return new Race
        {
            RaceId = Guid.NewGuid(),
            Name = $"Race {month}",
            Date = new DateTime(year, month, 1),
            Year = year,
            IsGrandPrixRace = true
        };
    }

    #endregion
}
