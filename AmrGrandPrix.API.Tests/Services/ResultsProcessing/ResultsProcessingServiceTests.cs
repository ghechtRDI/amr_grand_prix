using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs.RaceResults;
using AmrGrandPrix.API.Services.ResultsProcessing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace AmrGrandPrix.API.Tests.Services.ResultsProcessing;

public class ResultsProcessingServiceTests
{
    private readonly Mock<ILogger<ResultsProcessingService>> _loggerMock;
    private readonly ResultsProcessingService _service;

    public ResultsProcessingServiceTests()
    {
        _loggerMock = new Mock<ILogger<ResultsProcessingService>>();
        _service = new ResultsProcessingService(_loggerMock.Object);
    }

    #region Header Normalization Tests

    [Theory]
    [InlineData("Name", "Name")]
    [InlineData("name", "Name")]
    [InlineData("NAME", "Name")]
    [InlineData("First Name", "Name")]
    [InlineData("firstname", "Name")]
    [InlineData("Full Name", "Name")]
    [InlineData("Runner", "Name")]
    public void NormalizeHeaders_NameVariations_MapsToStandardName(string input, string expected)
    {
        // Arrange
        var headers = new[] { input };

        // Act
        var result = _service.NormalizeHeaders(headers);

        // Assert
        result.Should().ContainKey(input);
        result[input].Should().Be(expected);
    }

    [Theory]
    [InlineData("Age", "Age")]
    [InlineData("age", "Age")]
    [InlineData("Ag", "Age")]
    [InlineData("AGE", "Age")]
    public void NormalizeHeaders_AgeVariations_MapsToStandardAge(string input, string expected)
    {
        // Arrange
        var headers = new[] { input };

        // Act
        var result = _service.NormalizeHeaders(headers);

        // Assert
        result.Should().ContainKey(input);
        result[input].Should().Be(expected);
    }

    [Theory]
    [InlineData("Place", "Place")]
    [InlineData("Position", "Place")]
    [InlineData("Rank", "Place")]
    [InlineData("Overall", "Place")]
    [InlineData("pl", "Place")]
    public void NormalizeHeaders_PlaceVariations_MapsToStandardPlace(string input, string expected)
    {
        // Arrange
        var headers = new[] { input };

        // Act
        var result = _service.NormalizeHeaders(headers);

        // Assert
        result.Should().ContainKey(input);
        result[input].Should().Be(expected);
    }

    [Theory]
    [InlineData("Time", "Time")]
    [InlineData("Finish Time", "Time")]
    [InlineData("Clock Time", "Time")]
    [InlineData("Chip Time", "Time")]
    [InlineData("Gun Time", "Time")]
    public void NormalizeHeaders_TimeVariations_MapsToStandardTime(string input, string expected)
    {
        // Arrange
        var headers = new[] { input };

        // Act
        var result = _service.NormalizeHeaders(headers);

        // Assert
        result.Should().ContainKey(input);
        result[input].Should().Be(expected);
    }

    [Theory]
    [InlineData("Gender", "Gender")]
    [InlineData("Sex", "Gender")]
    [InlineData("M/F", "Gender")]
    public void NormalizeHeaders_GenderVariations_MapsToStandardGender(string input, string expected)
    {
        // Arrange
        var headers = new[] { input };

        // Act
        var result = _service.NormalizeHeaders(headers);

        // Assert
        result.Should().ContainKey(input);
        result[input].Should().Be(expected);
    }

    [Theory]
    [InlineData("Bib", "Bib")]
    [InlineData("Bib #", "Bib")]
    [InlineData("Bib Number", "Bib")]
    [InlineData("Number", "Bib")]
    public void NormalizeHeaders_BibVariations_MapsToStandardBib(string input, string expected)
    {
        // Arrange
        var headers = new[] { input };

        // Act
        var result = _service.NormalizeHeaders(headers);

        // Assert
        result.Should().ContainKey(input);
        result[input].Should().Be(expected);
    }

    #endregion

    #region Time Parsing Tests

    [Theory]
    [InlineData("1:23:45", 1, 23, 45, 0)]
    [InlineData("01:23:45", 1, 23, 45, 0)]
    [InlineData("2:15:30", 2, 15, 30, 0)]
    [InlineData("0:45:20", 0, 45, 20, 0)]
    public void ParseTime_HoursMinutesSeconds_ParsesCorrectly(string input, int hours, int minutes, int seconds, int milliseconds)
    {
        // Act
        var result = _service.ParseTime(input);

        // Assert
        result.Should().NotBeNull();
        result.Value.Hours.Should().Be(hours);
        result.Value.Minutes.Should().Be(minutes);
        result.Value.Seconds.Should().Be(seconds);
        result.Value.Milliseconds.Should().Be(milliseconds);
    }

    [Theory]
    [InlineData("23:45", 23, 45, 0)] // TimeSpan.Parse interprets as hours:minutes
    [InlineData("05:30", 5, 30, 0)]
    [InlineData("2:15", 2, 15, 0)]
    public void ParseTime_TwoComponents_ParsesAsHoursMinutes(string input, int expectedHours, int expectedMinutes, int expectedSeconds)
    {
        // Act
        var result = _service.ParseTime(input);

        // Assert
        result.Should().NotBeNull();
        result.Value.Hours.Should().Be(expectedHours);
        result.Value.Minutes.Should().Be(expectedMinutes);
        result.Value.Seconds.Should().Be(expectedSeconds);
    }

    [Theory]
    [InlineData("1:23:45.123", 123)]
    [InlineData("1:23:45.5", 500)]
    [InlineData("23:45.999", 999)]
    public void ParseTime_WithMilliseconds_ParsesCorrectly(string input, int expectedMilliseconds)
    {
        // Act
        var result = _service.ParseTime(input);

        // Assert
        result.Should().NotBeNull();
        result.Value.Milliseconds.Should().Be(expectedMilliseconds);
    }

    [Theory]
    [InlineData("DNF")]
    [InlineData("DNS")]
    [InlineData("DQ")]
    [InlineData("N/A")]
    [InlineData("")]
    [InlineData(null)]
    public void ParseTime_InvalidOrDNF_ReturnsNull(string? input)
    {
        // Act
        var result = _service.ParseTime(input);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseTime_StandardFormat_ParsesCorrectly()
    {
        // Arrange
        var timeString = "1:23:45";

        // Act
        var result = _service.ParseTime(timeString);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().Be(new TimeSpan(1, 23, 45));
    }

    #endregion

    #region Gender Detection Tests

    [Theory]
    [InlineData("MALE RESULTS", Gender.Male)]
    [InlineData("Male", Gender.Male)]
    [InlineData("MEN RESULTS", Gender.Male)]
    [InlineData("Men", Gender.Male)]
    [InlineData("BOYS DIVISION", Gender.Male)]
    public void DetectGenderFromSection_MalePatterns_ReturnsMale(string sectionHeader, Gender expected)
    {
        // Act
        var result = _service.DetectGenderFromSection(sectionHeader);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("FEMALE RESULTS", Gender.Female)]
    [InlineData("Female", Gender.Female)]
    [InlineData("WOMEN RESULTS", Gender.Female)]
    [InlineData("Women", Gender.Female)]
    [InlineData("GIRLS DIVISION", Gender.Female)]
    public void DetectGenderFromSection_FemalePatterns_ReturnsFemale(string sectionHeader, Gender expected)
    {
        // Act
        var result = _service.DetectGenderFromSection(sectionHeader);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("OVERALL RESULTS")]
    [InlineData("OPEN DIVISION")]
    public void DetectGenderFromSection_NoGenderPattern_ReturnsNull(string? sectionHeader)
    {
        // Act
        var result = _service.DetectGenderFromSection(sectionHeader);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void ValidateRow_ValidRow_ReturnsNoIssues()
    {
        // Arrange
        var row = new ResultRow
        {
            Name = "John Doe",
            Age = 35,
            Place = 1,
            Time = TimeSpan.FromHours(1.5),
            Gender = Gender.Male,
            Status = ResultStatus.Finished
        };

        // Act
        var result = _service.ValidateRow(row);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ValidateRow_MissingName_ReturnsError()
    {
        // Arrange
        var row = new ResultRow
        {
            Name = "",
            Age = 35,
            Place = 1,
            Time = TimeSpan.FromHours(1.5),
            Gender = Gender.Male
        };

        // Act
        var result = _service.ValidateRow(row);

        // Assert
        result.Should().Contain(i => i.Field == "Name" && i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void ValidateRow_MissingAge_ReturnsWarning()
    {
        // Arrange
        var row = new ResultRow
        {
            Name = "John Doe",
            Age = null,
            Place = 1,
            Time = TimeSpan.FromHours(1.5),
            Gender = Gender.Male
        };

        // Act
        var result = _service.ValidateRow(row);

        // Assert
        result.Should().Contain(i => i.Field == "Age" && i.Severity == ValidationSeverity.Warning);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(101)]
    public void ValidateRow_AgeOutOfRange_ReturnsWarning(int age)
    {
        // Arrange
        var row = new ResultRow
        {
            Name = "John Doe",
            Age = age,
            Place = 1,
            Time = TimeSpan.FromHours(1.5),
            Gender = Gender.Male
        };

        // Act
        var result = _service.ValidateRow(row);

        // Assert
        result.Should().Contain(i => i.Field == "Age" && i.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public void ValidateRow_MissingGender_ReturnsWarning()
    {
        // Arrange
        var row = new ResultRow
        {
            Name = "John Doe",
            Age = 35,
            Place = 1,
            Time = TimeSpan.FromHours(1.5),
            Gender = null
        };

        // Act
        var result = _service.ValidateRow(row);

        // Assert
        result.Should().Contain(i => i.Field == "Gender" && i.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public void ValidateRow_FinishedWithoutTime_ReturnsError()
    {
        // Arrange
        var row = new ResultRow
        {
            Name = "John Doe",
            Age = 35,
            Place = 1,
            Time = null,
            Gender = Gender.Male,
            Status = ResultStatus.Finished
        };

        // Act
        var result = _service.ValidateRow(row);

        // Assert
        result.Should().Contain(i => i.Field == "Time" && i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void ValidateRow_DNFWithoutTime_NoError()
    {
        // Arrange
        var row = new ResultRow
        {
            Name = "John Doe",
            Age = 35,
            Time = null,
            Gender = Gender.Male,
            Status = ResultStatus.DNF
        };

        // Act
        var result = _service.ValidateRow(row);

        // Assert
        result.Should().NotContain(i => i.Field == "Time" && i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void ValidateRow_ExcessiveTime_ReturnsWarning()
    {
        // Arrange
        var row = new ResultRow
        {
            Name = "John Doe",
            Age = 35,
            Place = 1,
            Time = TimeSpan.FromHours(25), // Over 24 hours
            Gender = Gender.Male,
            Status = ResultStatus.Finished
        };

        // Act
        var result = _service.ValidateRow(row);

        // Assert
        result.Should().Contain(i => i.Field == "Time" && i.Severity == ValidationSeverity.Warning);
    }

    #endregion

    #region Process Results Tests

    [Fact]
    public async Task ProcessResultsAsync_WithValidData_ProcessesSuccessfully()
    {
        // Arrange
        var rawRows = new List<RawResultRow>
        {
            new RawResultRow
            {
                RowNumber = 1,
                Columns = new Dictionary<string, string>
                {
                    { "Place", "1" },
                    { "Name", "John Doe" },
                    { "Age", "35" },
                    { "Gender", "M" },
                    { "Time", "1:23:45" }
                }
            }
        };

        // Act
        var result = await _service.ProcessResultsAsync(rawRows);

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("John Doe");
        result.First().Age.Should().Be(35);
        result.First().Place.Should().Be(1);
        result.First().Gender.Should().Be(Gender.Male);
        result.First().Time.Should().Be(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(23) + TimeSpan.FromSeconds(45));
    }

    [Fact]
    public async Task ProcessResultsAsync_WithSectionHeader_DetectsGender()
    {
        // Arrange
        var rawRows = new List<RawResultRow>
        {
            new RawResultRow
            {
                RowNumber = 1,
                SectionHeader = "MALE RESULTS",
                Columns = new Dictionary<string, string>
                {
                    { "Place", "1" },
                    { "Name", "John Doe" },
                    { "Age", "35" },
                    { "Time", "1:23:45" }
                }
            }
        };

        // Act
        var result = await _service.ProcessResultsAsync(rawRows);

        // Assert
        result.First().Gender.Should().Be(Gender.Male);
    }

    [Fact]
    public async Task ProcessResultsAsync_WithDNF_SetsCorrectStatus()
    {
        // Arrange
        var rawRows = new List<RawResultRow>
        {
            new RawResultRow
            {
                RowNumber = 1,
                Columns = new Dictionary<string, string>
                {
                    { "Name", "John Doe" },
                    { "Age", "35" },
                    { "Gender", "M" },
                    { "Time", "DNF" }
                }
            }
        };

        // Act
        var result = await _service.ProcessResultsAsync(rawRows);

        // Assert
        result.First().Status.Should().Be(ResultStatus.DNF);
        result.First().Time.Should().BeNull();
    }

    [Fact]
    public async Task ProcessResultsAsync_AutoDetectsColumnMappings_WhenNotProvided()
    {
        // Arrange
        var rawRows = new List<RawResultRow>
        {
            new RawResultRow
            {
                RowNumber = 1,
                Columns = new Dictionary<string, string>
                {
                    { "Position", "1" },  // Different header name
                    { "Runner", "John Doe" },  // Different header name
                    { "Ag", "35" },  // Different header name
                    { "M/F", "M" },  // Different header name
                    { "Finish Time", "1:23:45" }  // Different header name
                }
            }
        };

        // Act
        var result = await _service.ProcessResultsAsync(rawRows);

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("John Doe");
        result.First().Age.Should().Be(35);
        result.First().Place.Should().Be(1);
        result.First().Gender.Should().Be(Gender.Male);
        result.First().Time.Should().NotBeNull();
    }

    #endregion
}
