using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs.RaceResults;
using AmrGrandPrix.API.Services.LlmExtraction;
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
        _service    = new ResultsProcessingService(_loggerMock.Object);
    }

    // ── Time Parsing ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("1:23:45",  1, 23, 45, 0)]
    [InlineData("01:23:45", 1, 23, 45, 0)]
    [InlineData("2:15:30",  2, 15, 30, 0)]
    [InlineData("0:45:20",  0, 45, 20, 0)]
    public void ParseTime_HoursMinutesSeconds_ParsesCorrectly(
        string input, int hours, int minutes, int seconds, int milliseconds)
    {
        var result = _service.ParseTime(input);

        result.Should().NotBeNull();
        result!.Value.Hours.Should().Be(hours);
        result.Value.Minutes.Should().Be(minutes);
        result.Value.Seconds.Should().Be(seconds);
        result.Value.Milliseconds.Should().Be(milliseconds);
    }

    [Theory]
    [InlineData("23:45", 23, 45, 0)]
    [InlineData("05:30",  5, 30, 0)]
    [InlineData("2:15",   2, 15, 0)]
    public void ParseTime_TwoComponents_ParsesAsHoursMinutes(
        string input, int expectedHours, int expectedMinutes, int expectedSeconds)
    {
        var result = _service.ParseTime(input);

        result.Should().NotBeNull();
        result!.Value.Hours.Should().Be(expectedHours);
        result.Value.Minutes.Should().Be(expectedMinutes);
        result.Value.Seconds.Should().Be(expectedSeconds);
    }

    [Theory]
    [InlineData("1:23:45.123", 123)]
    [InlineData("1:23:45.5",   500)]
    [InlineData("23:45.999",   999)]
    public void ParseTime_WithMilliseconds_ParsesCorrectly(string input, int expectedMs)
    {
        var result = _service.ParseTime(input);

        result.Should().NotBeNull();
        result!.Value.Milliseconds.Should().Be(expectedMs);
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
        _service.ParseTime(input).Should().BeNull();
    }

    [Fact]
    public void ParseTime_StandardFormat_ParsesCorrectly()
    {
        _service.ParseTime("1:23:45").Should().Be(new TimeSpan(1, 23, 45));
    }

    // ── Gender Detection ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("MALE RESULTS",  Gender.Male)]
    [InlineData("Male",          Gender.Male)]
    [InlineData("MEN RESULTS",   Gender.Male)]
    [InlineData("Men",           Gender.Male)]
    [InlineData("BOYS DIVISION", Gender.Male)]
    public void DetectGenderFromSection_MalePatterns_ReturnsMale(string header, Gender expected)
    {
        _service.DetectGenderFromSection(header).Should().Be(expected);
    }

    [Theory]
    [InlineData("FEMALE RESULTS",  Gender.Female)]
    [InlineData("Female",          Gender.Female)]
    [InlineData("WOMEN RESULTS",   Gender.Female)]
    [InlineData("Women",           Gender.Female)]
    [InlineData("GIRLS DIVISION",  Gender.Female)]
    public void DetectGenderFromSection_FemalePatterns_ReturnsFemale(string header, Gender expected)
    {
        _service.DetectGenderFromSection(header).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("OVERALL RESULTS")]
    [InlineData("OPEN DIVISION")]
    public void DetectGenderFromSection_NoGenderPattern_ReturnsNull(string? header)
    {
        _service.DetectGenderFromSection(header).Should().BeNull();
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateRow_ValidRow_ReturnsNoIssues()
    {
        var row = new ResultRow
        {
            Name   = "John Doe",
            Age    = 35,
            Place  = 1,
            Time   = TimeSpan.FromHours(1.5),
            Gender = Gender.Male,
            Status = ResultStatus.Finished
        };

        _service.ValidateRow(row).Should().BeEmpty();
    }

    [Fact]
    public void ValidateRow_MissingName_ReturnsError()
    {
        var row = new ResultRow { Name = "", Age = 35, Place = 1, Time = TimeSpan.FromHours(1.5), Gender = Gender.Male };
        _service.ValidateRow(row)
            .Should().Contain(i => i.Field == "Name" && i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void ValidateRow_MissingAge_ReturnsWarning()
    {
        var row = new ResultRow { Name = "John Doe", Age = null, Place = 1, Time = TimeSpan.FromHours(1.5), Gender = Gender.Male };
        _service.ValidateRow(row)
            .Should().Contain(i => i.Field == "Age" && i.Severity == ValidationSeverity.Warning);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(101)]
    public void ValidateRow_AgeOutOfRange_ReturnsWarning(int age)
    {
        var row = new ResultRow { Name = "John Doe", Age = age, Place = 1, Time = TimeSpan.FromHours(1.5), Gender = Gender.Male };
        _service.ValidateRow(row)
            .Should().Contain(i => i.Field == "Age" && i.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public void ValidateRow_MissingGender_ReturnsWarning()
    {
        var row = new ResultRow { Name = "John Doe", Age = 35, Place = 1, Time = TimeSpan.FromHours(1.5), Gender = null };
        _service.ValidateRow(row)
            .Should().Contain(i => i.Field == "Gender" && i.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public void ValidateRow_FinishedWithoutTime_ReturnsError()
    {
        var row = new ResultRow { Name = "John Doe", Age = 35, Place = 1, Time = null, Gender = Gender.Male, Status = ResultStatus.Finished };
        _service.ValidateRow(row)
            .Should().Contain(i => i.Field == "Time" && i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void ValidateRow_DNFWithoutTime_NoError()
    {
        var row = new ResultRow { Name = "John Doe", Age = 35, Time = null, Gender = Gender.Male, Status = ResultStatus.DNF };
        _service.ValidateRow(row)
            .Should().NotContain(i => i.Field == "Time" && i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void ValidateRow_ExcessiveTime_ReturnsWarning()
    {
        var row = new ResultRow { Name = "John Doe", Age = 35, Place = 1, Time = TimeSpan.FromHours(25), Gender = Gender.Male, Status = ResultStatus.Finished };
        _service.ValidateRow(row)
            .Should().Contain(i => i.Field == "Time" && i.Severity == ValidationSeverity.Warning);
    }

    // ── ProcessResultsAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task ProcessResultsAsync_WithValidData_ProcessesSuccessfully()
    {
        var sections = new List<ExtractedSection>
        {
            new(null, null, new List<ExtractedRow>
            {
                new(Place: 1, Name: "John Doe", Age: 35, AgeCategory: null, Gender: "Male",
                    TimeString: "1:23:45", Status: "Finished", Notes: null)
            })
        };

        var result = await _service.ProcessResultsAsync(sections);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("John Doe");
        result[0].Age.Should().Be(35);
        result[0].Place.Should().Be(1);
        result[0].Gender.Should().Be(Gender.Male);
        result[0].Time.Should().Be(new TimeSpan(1, 23, 45));
    }

    [Fact]
    public async Task ProcessResultsAsync_WithSectionGender_UsesGenderFallback()
    {
        var sections = new List<ExtractedSection>
        {
            new("MALE RESULTS", "Male", new List<ExtractedRow>
            {
                new(Place: 1, Name: "John Doe", Age: 35, AgeCategory: null, Gender: null,
                    TimeString: "1:23:45", Status: "Finished", Notes: null)
            })
        };

        var result = await _service.ProcessResultsAsync(sections);

        result[0].Gender.Should().Be(Gender.Male);
    }

    [Fact]
    public async Task ProcessResultsAsync_WithDNFStatus_SetsCorrectStatus()
    {
        var sections = new List<ExtractedSection>
        {
            new(null, null, new List<ExtractedRow>
            {
                new(Place: null, Name: "John Doe", Age: 35, AgeCategory: null, Gender: "Male",
                    TimeString: null, Status: "DNF", Notes: null)
            })
        };

        var result = await _service.ProcessResultsAsync(sections);

        result[0].Status.Should().Be(ResultStatus.DNF);
        result[0].Time.Should().BeNull();
    }

    [Fact]
    public async Task ProcessResultsAsync_LastFirstName_ConvertsToFirstLast()
    {
        // If > 50% of names have commas, treat as Last, First
        var rows = Enumerable.Range(1, 5).Select(i =>
            new ExtractedRow(i, $"Doe{i}, John", 30, null, "Male", "1:00:00", "Finished", null)).ToList();

        var sections = new List<ExtractedSection> { new(null, null, rows) };
        var result = await _service.ProcessResultsAsync(sections);

        result[0].Name.Should().Be("John Doe1");
    }

    [Fact]
    public async Task ProcessResultsAsync_MultipleSections_FlattenedToSingleList()
    {
        var sections = new List<ExtractedSection>
        {
            new("MALE RESULTS", "Male", new List<ExtractedRow>
            {
                new(1, "John Doe", 35, null, null, "1:23:45", "Finished", null)
            }),
            new("FEMALE RESULTS", "Female", new List<ExtractedRow>
            {
                new(1, "Jane Smith", 28, null, null, "1:30:00", "Finished", null)
            })
        };

        var result = await _service.ProcessResultsAsync(sections);

        result.Should().HaveCount(2);
        result[0].Gender.Should().Be(Gender.Male);
        result[1].Gender.Should().Be(Gender.Female);
    }

    // ── Age Category Mapping ──────────────────────────────────────────────────

    [Theory]
    [InlineData(30, "30-39")]
    [InlineData(40, "40-49")]
    [InlineData(17, "17 and Under")]
    [InlineData(80, "80-89")]
    public void AgeCategory_MapsCorrectly(int age, string expectedCategory)
    {
        var calcService = new AmrGrandPrix.API.Services.GrandPrix.GrandPrixCalculationService(null!, null!);
        calcService.DetermineAgeCategory(age).Should().Be(expectedCategory);
    }
}
