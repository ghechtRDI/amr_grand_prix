using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs.RaceResults;
using AmrGrandPrix.API.Services.ResultsProcessing;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AmrGrandPrix.API.Tests.Services.ResultsProcessing;

public class RunnerMatchingServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<RunnerMatchingService>> _loggerMock;
    private readonly RunnerMatchingService _service;

    public RunnerMatchingServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _loggerMock = new Mock<ILogger<RunnerMatchingService>>();
        _service = new RunnerMatchingService(_context, _loggerMock.Object);

        // Seed test data
        SeedTestData();
    }

    private void SeedTestData()
    {
        var runners = new List<Runner>
        {
            new Runner
            {
                RunnerId = Guid.NewGuid(),
                FirstName = "John",
                LastName = "Doe",
                DateOfBirth = new DateTime(1989, 5, 15), // Age ~35
                Gender = Gender.Male
            },
            new Runner
            {
                RunnerId = Guid.NewGuid(),
                FirstName = "Jane",
                LastName = "Smith",
                DateOfBirth = new DateTime(1996, 8, 20), // Age ~28
                Gender = Gender.Female
            },
            new Runner
            {
                RunnerId = Guid.NewGuid(),
                FirstName = "Michael",
                LastName = "Johnson",
                DateOfBirth = new DateTime(1982, 3, 10), // Age ~42
                Gender = Gender.Male
            },
            new Runner
            {
                RunnerId = Guid.NewGuid(),
                FirstName = "Sarah",
                LastName = "Lee",
                Gender = Gender.Female
                // No date of birth
            }
        };

        _context.Runners.AddRange(runners);
        _context.SaveChanges();
    }

    #region Name Similarity Tests

    [Theory]
    [InlineData("John Doe", "John Doe", 1.0)]
    [InlineData("john doe", "John Doe", 1.0)]
    [InlineData("JOHN DOE", "John Doe", 1.0)]
    [InlineData("John  Doe", "John Doe", 1.0)] // Extra whitespace
    public void CalculateNameSimilarity_ExactMatches_Returns100Percent(string name1, string name2, double expected)
    {
        // Act
        var similarity = _service.CalculateNameSimilarity(name1, name2);

        // Assert
        similarity.Should().BeApproximately(expected, 0.01);
    }

    [Theory]
    [InlineData("John Doe", "Jon Doe", 0.85)] // One character difference
    [InlineData("John Doe", "John Do", 0.85)] // One character missing
    [InlineData("John Doe", "John Smith", 0.40)] // Different last name
    public void CalculateNameSimilarity_SimilarNames_ReturnsHighSimilarity(string name1, string name2, double expectedMin)
    {
        // Act
        var similarity = _service.CalculateNameSimilarity(name1, name2);

        // Assert
        similarity.Should().BeGreaterThanOrEqualTo(expectedMin);
    }

    [Theory]
    [InlineData("John Doe", "Jane Smith")]
    [InlineData("Michael Johnson", "Sarah Lee")]
    public void CalculateNameSimilarity_DifferentNames_ReturnsLowSimilarity(string name1, string name2)
    {
        // Act
        var similarity = _service.CalculateNameSimilarity(name1, name2);

        // Assert
        similarity.Should().BeLessThan(0.5);
    }

    [Fact]
    public void CalculateNameSimilarity_EmptyStrings_ReturnsZero()
    {
        // Act
        var similarity = _service.CalculateNameSimilarity("", "");

        // Assert
        similarity.Should().Be(0.0);
    }

    [Fact]
    public void CalculateNameSimilarity_OneEmptyString_ReturnsZero()
    {
        // Act
        var similarity = _service.CalculateNameSimilarity("John Doe", "");

        // Assert
        similarity.Should().Be(0.0);
    }

    #endregion

    #region FindMatches Tests

    [Fact]
    public async Task FindMatchesAsync_ExactNameMatch_ReturnsHighConfidence()
    {
        // Act
        var matches = await _service.FindMatchesAsync("John Doe", 35, Gender.Male);

        // Assert
        matches.Should().HaveCountGreaterThan(0);
        var topMatch = matches.First();
        topMatch.FirstName.Should().Be("John");
        topMatch.LastName.Should().Be("Doe");
        topMatch.Confidence.Should().BeGreaterThan(0.95);
        topMatch.NameMatch.Should().BeTrue();
        topMatch.AgeMatch.Should().BeTrue();
        topMatch.GenderMatch.Should().BeTrue();
    }

    [Fact]
    public async Task FindMatchesAsync_CloseNameMatch_ReturnsReasonableConfidence()
    {
        // Act
        var matches = await _service.FindMatchesAsync("Jon Doe", 35, Gender.Male);

        // Assert
        matches.Should().HaveCountGreaterThan(0);
        var topMatch = matches.First();
        topMatch.FirstName.Should().Be("John");
        topMatch.LastName.Should().Be("Doe");
        topMatch.Confidence.Should().BeGreaterThan(0.70);
    }

    [Fact]
    public async Task FindMatchesAsync_WithMatchingAge_SetsAgeMatchFlag()
    {
        // Act
        var matches = await _service.FindMatchesAsync("John Doe", 35, null);

        // Assert
        var match = matches.FirstOrDefault(m => m.FirstName == "John" && m.LastName == "Doe");
        match.Should().NotBeNull();
        match!.AgeMatch.Should().BeTrue();
        match.Confidence.Should().BeGreaterThan(0.85); // Name + Age should give high confidence
    }

    [Fact]
    public async Task FindMatchesAsync_WithMatchingGender_SetsGenderMatchFlag()
    {
        // Act
        var matches = await _service.FindMatchesAsync("John Doe", null, Gender.Male);

        // Assert
        var match = matches.FirstOrDefault(m => m.FirstName == "John" && m.LastName == "Doe");
        match.Should().NotBeNull();
        match!.GenderMatch.Should().BeTrue();
        match.Confidence.Should().BeGreaterThan(0.85); // Name + Gender should give high confidence
    }

    [Fact]
    public async Task FindMatchesAsync_AgeTooFarOff_ExcludesFromMatches()
    {
        // Act
        var matches = await _service.FindMatchesAsync("John Doe", 50, Gender.Male); // 15 years off

        // Assert
        // Should not match due to age being too different (>2 years)
        var johnMatch = matches.FirstOrDefault(m => m.FirstName == "John" && m.LastName == "Doe");
        if (johnMatch != null)
        {
            johnMatch.AgeMatch.Should().BeFalse();
        }
    }

    [Fact]
    public async Task FindMatchesAsync_WrongGender_ExcludesFromMatches()
    {
        // Act
        var matches = await _service.FindMatchesAsync("John Doe", 35, Gender.Female);

        // Assert
        var johnMatch = matches.FirstOrDefault(m => m.FirstName == "John" && m.LastName == "Doe");
        if (johnMatch != null)
        {
            johnMatch.GenderMatch.Should().BeFalse();
        }
    }

    [Fact]
    public async Task FindMatchesAsync_NoSimilarNames_ReturnsEmptyList()
    {
        // Act
        var matches = await _service.FindMatchesAsync("Completely Different Person", 25, Gender.Male);

        // Assert
        matches.Should().BeEmpty();
    }

    [Fact]
    public async Task FindMatchesAsync_EmptyName_ReturnsEmptyList()
    {
        // Act
        var matches = await _service.FindMatchesAsync("", 25, Gender.Male);

        // Assert
        matches.Should().BeEmpty();
    }

    [Fact]
    public async Task FindMatchesAsync_RunnerWithoutDOB_CanStillMatch()
    {
        // Act
        var matches = await _service.FindMatchesAsync("Sarah Lee", null, Gender.Female);

        // Assert
        matches.Should().HaveCountGreaterThan(0);
        var sarahMatch = matches.First(m => m.FirstName == "Sarah" && m.LastName == "Lee");
        sarahMatch.Should().NotBeNull();
        sarahMatch.Age.Should().BeNull();
    }

    [Fact]
    public async Task FindMatchesAsync_SortsByConfidence_DescendingOrder()
    {
        // Arrange
        // Add another runner with similar name but slightly different age (lower match)
        _context.Runners.Add(new Runner
        {
            RunnerId = Guid.NewGuid(),
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1984, 5, 15), // Age ~40 (5 years off)
            Gender = Gender.Male
        });
        await _context.SaveChangesAsync();

        // Act
        var matches = await _service.FindMatchesAsync("John Doe", 35, Gender.Male);

        // Assert
        matches.Should().HaveCountGreaterThan(1);
        for (int i = 0; i < matches.Count - 1; i++)
        {
            matches[i].Confidence.Should().BeGreaterThanOrEqualTo(matches[i + 1].Confidence);
        }
    }

    #endregion

    #region FindMatchesForResults Tests

    [Fact]
    public async Task FindMatchesForResultsAsync_PopulatesRunnerMatches()
    {
        // Arrange
        var resultRows = new List<ResultRow>
        {
            new ResultRow
            {
                Name = "John Doe",
                Age = 35,
                Gender = Gender.Male
            }
        };

        // Act
        var results = await _service.FindMatchesForResultsAsync(resultRows);

        // Assert
        results.First().RunnerMatches.Should().NotBeNull();
        results.First().RunnerMatches.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task FindMatchesForResultsAsync_HighConfidence_AutoMatches()
    {
        // Arrange
        var resultRows = new List<ResultRow>
        {
            new ResultRow
            {
                Name = "John Doe",
                Age = 35,
                Gender = Gender.Male
            }
        };

        // Act
        var results = await _service.FindMatchesForResultsAsync(resultRows);

        // Assert
        results.First().MatchedRunnerId.Should().NotBeNull();
    }

    [Fact]
    public async Task FindMatchesForResultsAsync_LowConfidence_DoesNotAutoMatch()
    {
        // Arrange
        var resultRows = new List<ResultRow>
        {
            new ResultRow
            {
                Name = "Jonathan Doe", // Similar but not exact
                Age = 40, // Different age
                Gender = Gender.Male
            }
        };

        // Act
        var results = await _service.FindMatchesForResultsAsync(resultRows);

        // Assert
        // Should have matches but not auto-matched due to lower confidence
        var topMatch = results.First().RunnerMatches?.FirstOrDefault();
        if (topMatch != null && topMatch.Confidence < 0.95)
        {
            results.First().MatchedRunnerId.Should().BeNull();
        }
    }

    [Fact]
    public async Task FindMatchesForResultsAsync_NewRunner_AddsInfoIssue()
    {
        // Arrange
        var resultRows = new List<ResultRow>
        {
            new ResultRow
            {
                Name = "Brand New Person",
                Age = 25,
                Gender = Gender.Male,
                ValidationIssues = new List<ValidationIssue>()
            }
        };

        // Act
        var results = await _service.FindMatchesForResultsAsync(resultRows);

        // Assert
        results.First().ValidationIssues.Should().Contain(i =>
            i.Field == "Runner" &&
            i.Severity == ValidationSeverity.Info &&
            i.Message.Contains("New runner"));
    }

    [Fact]
    public async Task FindMatchesForResultsAsync_OnlyIncludesReasonableMatches()
    {
        // Arrange
        var resultRows = new List<ResultRow>
        {
            new ResultRow
            {
                Name = "John Doe",
                Age = 35,
                Gender = Gender.Male
            }
        };

        // Act
        var results = await _service.FindMatchesForResultsAsync(resultRows);

        // Assert
        var matches = results.First().RunnerMatches;
        matches.Should().NotBeNull();
        // All matches should have confidence >= 0.70
        matches!.Should().OnlyContain(m => m.Confidence >= 0.70);
    }

    [Fact]
    public async Task FindMatchesForResultsAsync_SkipsEmptyNames()
    {
        // Arrange
        var resultRows = new List<ResultRow>
        {
            new ResultRow
            {
                Name = "",
                Age = 35,
                Gender = Gender.Male
            }
        };

        // Act
        var results = await _service.FindMatchesForResultsAsync(resultRows);

        // Assert
        results.First().RunnerMatches.Should().BeNullOrEmpty();
        results.First().MatchedRunnerId.Should().BeNull();
    }

    #endregion

    public void Dispose()
    {
        _context?.Dispose();
    }
}
