using AmrGrandPrix.API.Models.DTOs.RaceResults;
using ClosedXML.Excel;

namespace AmrGrandPrix.API.Services.FileParser;

/// <summary>
/// Service for parsing Excel (.xlsx, .xls) race result files
/// </summary>
public class ExcelParserService : IFileParserService
{
    private readonly ILogger<ExcelParserService> _logger;

    public ExcelParserService(ILogger<ExcelParserService> logger)
    {
        _logger = logger;
    }

    public bool CanParse(string fileName)
    {
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        return extension is ".xlsx" or ".xls";
    }

    public async Task<List<RawResultRow>> ParseAsync(Stream fileStream, string fileName)
    {
        _logger.LogInformation("Parsing Excel file: {FileName}", fileName);

        var rows = new List<RawResultRow>();
        string? currentSection = null;

        try
        {
            // ClosedXML doesn't have async methods, so we run synchronously
            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook(fileStream);

                // Get the first worksheet
                var worksheet = workbook.Worksheets.First();
                _logger.LogInformation("Processing worksheet: {WorksheetName}", worksheet.Name);

                // Find the header row (first non-empty row)
                var firstRow = worksheet.FirstRowUsed();
                if (firstRow == null)
                {
                    _logger.LogWarning("Worksheet is empty");
                    return;
                }

                var headerRow = firstRow;
                var headers = new List<string>();

                // Read headers from first row
                foreach (var cell in headerRow.CellsUsed())
                {
                    var headerValue = cell.GetString().Trim();
                    headers.Add(headerValue);
                }

                if (headers.Count == 0)
                {
                    _logger.LogWarning("No headers found in Excel file");
                    return;
                }

                _logger.LogInformation("Detected {Count} columns: {Headers}", headers.Count, string.Join(", ", headers));

                // Read data rows
                int rowNumber = 1;
                foreach (var row in worksheet.RowsUsed().Skip(1)) // Skip header row
                {
                    rowNumber++;

                    try
                    {
                        var columns = new Dictionary<string, string>();
                        var cellValues = row.CellsUsed().ToList();

                        // Check if first cell might be a section header
                        if (cellValues.Count > 0)
                        {
                            var firstCellValue = cellValues[0].GetString().Trim();
                            if (IsSectionHeader(firstCellValue))
                            {
                                currentSection = firstCellValue;
                                _logger.LogDebug("Detected section header: {Section}", currentSection);
                                continue; // Skip this row
                            }
                        }

                        // Map cells to headers
                        for (int i = 0; i < headers.Count && i < cellValues.Count; i++)
                        {
                            var headerName = headers[i];
                            var cellValue = cellValues[i].GetString().Trim();
                            columns[headerName] = cellValue;
                        }

                        // Only add if we have actual data
                        if (columns.Count > 0 && columns.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
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
            });

            _logger.LogInformation("Successfully parsed {Count} rows from Excel", rows.Count);
            return rows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Excel file: {FileName}", fileName);
            throw new InvalidOperationException($"Failed to parse Excel file: {ex.Message}", ex);
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
