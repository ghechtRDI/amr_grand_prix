using System.Text;
using AmrGrandPrix.API.Services.FileParser;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace AmrGrandPrix.API.Tests.Services.FileParser;

public class CsvParserServiceTests
{
    private readonly Mock<ILogger<CsvParserService>> _loggerMock;
    private readonly CsvParserService _service;

    public CsvParserServiceTests()
    {
        _loggerMock = new Mock<ILogger<CsvParserService>>();
        _service = new CsvParserService(_loggerMock.Object);
    }

    [Fact]
    public void CanParse_WithCsvExtension_ReturnsTrue()
    {
        // Arrange
        var fileName = "results.csv";

        // Act
        var result = _service.CanParse(fileName);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanParse_WithTxtExtension_ReturnsTrue()
    {
        // Arrange
        var fileName = "results.txt";

        // Act
        var result = _service.CanParse(fileName);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanParse_WithOtherExtension_ReturnsFalse()
    {
        // Arrange
        var fileName = "results.xlsx";

        // Act
        var result = _service.CanParse(fileName);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ParseAsync_WithValidCsv_ReturnsCorrectNumberOfRows()
    {
        // Arrange
        var csvContent = @"Place,Bib,Name,Age,Gender,Time
1,42,John Doe,35,M,1:23:45
2,17,Jane Smith,28,F,1:25:12
3,99,Mike Johnson,42,M,1:27:03";

        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _service.ParseAsync(stream, "results.csv");

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task ParseAsync_WithValidCsv_ParsesColumnsCorrectly()
    {
        // Arrange
        var csvContent = @"Place,Bib,Name,Age,Gender,Time
1,42,John Doe,35,M,1:23:45";

        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _service.ParseAsync(stream, "results.csv");

        // Assert
        var row = result.First();
        row.Columns.Should().ContainKey("Place");
        row.Columns.Should().ContainKey("Bib");
        row.Columns.Should().ContainKey("Name");
        row.Columns.Should().ContainKey("Age");
        row.Columns.Should().ContainKey("Gender");
        row.Columns.Should().ContainKey("Time");

        row.Columns["Place"].Should().Be("1");
        row.Columns["Bib"].Should().Be("42");
        row.Columns["Name"].Should().Be("John Doe");
        row.Columns["Age"].Should().Be("35");
        row.Columns["Gender"].Should().Be("M");
        row.Columns["Time"].Should().Be("1:23:45");
    }

    [Fact]
    public async Task ParseAsync_WithSectionHeaders_DetectsSections()
    {
        // Arrange
        var csvContent = @"Place,Bib,Name,Age,Time
MALE RESULTS,,,
1,42,John Doe,35,1:23:45
2,99,Mike Johnson,42,1:27:03
FEMALE RESULTS,,,
1,17,Jane Smith,28,1:25:12";

        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _service.ParseAsync(stream, "results.csv");

        // Assert
        result.Should().HaveCountGreaterThan(0);
        var firstMaleResult = result.FirstOrDefault(r => r.Columns.ContainsKey("Name") && r.Columns["Name"] == "John Doe");
        firstMaleResult.Should().NotBeNull();
        firstMaleResult!.SectionHeader.Should().Contain("MALE");
    }

    [Fact]
    public async Task ParseAsync_WithEmptyFile_ThrowsException()
    {
        // Arrange
        var csvContent = "";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act & Assert
        await FluentActions.Invoking(async () => await _service.ParseAsync(stream, "results.csv"))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ParseAsync_WithHeaderOnly_ReturnsEmptyList()
    {
        // Arrange
        var csvContent = "Place,Bib,Name,Age,Gender,Time";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _service.ParseAsync(stream, "results.csv");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_WithTabDelimiter_ParsesCorrectly()
    {
        // Arrange
        var csvContent = "Place\tBib\tName\tAge\tGender\tTime\n1\t42\tJohn Doe\t35\tM\t1:23:45";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _service.ParseAsync(stream, "results.csv");

        // Assert
        result.Should().HaveCount(1);
        result.First().Columns["Name"].Should().Be("John Doe");
    }

    [Fact]
    public async Task ParseAsync_WithMissingValues_HandlesGracefully()
    {
        // Arrange
        var csvContent = @"Place,Bib,Name,Age,Gender,Time
1,42,John Doe,35,,1:23:45
2,,Jane Smith,28,F,";

        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _service.ParseAsync(stream, "results.csv");

        // Assert
        result.Should().HaveCount(2);
        result.First().Columns["Gender"].Should().BeEmpty();
        result.Last().Columns["Bib"].Should().BeEmpty();
        result.Last().Columns["Time"].Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_WithExtraWhitespace_TrimsValues()
    {
        // Arrange
        var csvContent = @"Place,Bib,Name,Age,Gender,Time
1,42,  John Doe  ,35,M,1:23:45";

        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _service.ParseAsync(stream, "results.csv");

        // Assert
        result.First().Columns["Name"].Should().Be("John Doe");
    }

    [Fact]
    public async Task ParseAsync_SetsRowNumbers_Correctly()
    {
        // Arrange
        var csvContent = @"Place,Bib,Name,Age,Gender,Time
1,42,John Doe,35,M,1:23:45
2,17,Jane Smith,28,F,1:25:12
3,99,Mike Johnson,42,M,1:27:03";

        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _service.ParseAsync(stream, "results.csv");

        // Assert
        result[0].RowNumber.Should().Be(2); // First data row (after header)
        result[1].RowNumber.Should().Be(3);
        result[2].RowNumber.Should().Be(4);
    }

    [Fact]
    public async Task ParseAsync_WithQuotedFields_HandlesCorrectly()
    {
        // Arrange
        var csvContent = @"Place,Bib,Name,Age,Gender,Time
1,42,""Doe, John"",35,M,1:23:45
2,17,""Smith, Jane"",28,F,1:25:12";

        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _service.ParseAsync(stream, "results.csv");

        // Assert
        result.Should().HaveCount(2);
        result.First().Columns["Name"].Should().Be("Doe, John");
        result.Last().Columns["Name"].Should().Be("Smith, Jane");
    }

    [Fact]
    public async Task ParseAsync_LoadsRealTestFile_Successfully()
    {
        // Arrange
        var filePath = Path.Combine("TestData", "sample_race_results.csv");

        if (!File.Exists(filePath))
        {
            // Skip test if file doesn't exist
            return;
        }

        using var stream = File.OpenRead(filePath);

        // Act
        var result = await _service.ParseAsync(stream, "sample_race_results.csv");

        // Assert
        result.Should().HaveCountGreaterThan(0);
        result.Should().Contain(r => r.Columns.ContainsKey("Name") && r.Columns["Name"] == "John Doe");
    }
}
