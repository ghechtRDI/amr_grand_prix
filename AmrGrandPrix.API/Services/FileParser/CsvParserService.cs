using System.Globalization;
using AmrGrandPrix.API.Models.DTOs.RaceResults;
using CsvHelper;
using CsvHelper.Configuration;

namespace AmrGrandPrix.API.Services.FileParser;

/// <summary>
/// Service for parsing CSV race result files
/// </summary>
public class CsvParserService : IFileParserService
{
    private readonly ILogger<CsvParserService> _logger;

    public CsvParserService(ILogger<CsvParserService> logger)
    {
        _logger = logger;
    }

    public bool CanParse(string fileName)
    {
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        return extension is ".csv" or ".txt";
    }

    public async Task<List<RawResultRow>> ParseAsync(Stream fileStream, string fileName)
    {
        _logger.LogInformation("Parsing CSV file: {FileName}", fileName);

        var rows = new List<RawResultRow>();
        string? currentSection = null;

        try
        {
            using var reader = new StreamReader(fileStream);

            // Try to detect if there are section headers (e.g., "MALE RESULTS")
            var content = await reader.ReadToEndAsync();
            fileStream.Position = 0;

            var lines = content.Split('\n');
            var hasDetectedHeaders = false;

            using var csvReader = new StreamReader(fileStream);
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                BadDataFound = null,
                HeaderValidated = null,
                TrimOptions = TrimOptions.Trim,
                DetectDelimiter = true
            };

            using var csv = new CsvReader(csvReader, config);

            // Read header
            await csv.ReadAsync();
            csv.ReadHeader();
            var headers = csv.HeaderRecord ?? Array.Empty<string>();

            if (headers.Length == 0)
            {
                _logger.LogWarning("No headers found in CSV file");
                return rows;
            }

            _logger.LogInformation("Detected {Count} columns: {Headers}", headers.Length, string.Join(", ", headers));

            int rowNumber = 1; // Start at 1 (header is row 0)

            while (await csv.ReadAsync())
            {
                rowNumber++;

                try
                {
                    var columns = new Dictionary<string, string>();

                    for (int i = 0; i < headers.Length; i++)
                    {
                        var headerName = headers[i];
                        var value = csv.GetField(i) ?? string.Empty;

                        // Check if this row is actually a section header
                        if (i == 0 && IsSectionHeader(value))
                        {
                            currentSection = value;
                            _logger.LogDebug("Detected section header: {Section}", currentSection);
                            break; // Skip this row
                        }

                        columns[headerName] = value;
                    }

                    // Only add if we have actual data (not a section header row)
                    if (columns.Count > 0)
                    {
                        rows.Add(new RawResultRow
                        {
                            RowNumber = rowNumber,
                            Columns = columns,
                            SectionHeader = currentSection
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing row {RowNumber}, skipping", rowNumber);
                }
            }

            _logger.LogInformation("Successfully parsed {Count} rows from CSV", rows.Count);
            return rows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse CSV file: {FileName}", fileName);
            throw new InvalidOperationException($"Failed to parse CSV file: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Detects if a line is a section header (e.g., "MALE RESULTS", "FEMALE RESULTS")
    /// </summary>
    private bool IsSectionHeader(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        var upperLine = line.ToUpperInvariant().Trim();

        var sectionPatterns = new[]
        {
            "MALE", "FEMALE", "MEN", "WOMEN",
            "MAN", "WOMAN", "BOYS", "GIRLS",
            "RESULTS", "DIVISION"
        };

        // Check if line contains any section keywords
        return sectionPatterns.Any(pattern => upperLine.Contains(pattern));
    }
}
