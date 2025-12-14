using System.Text.RegularExpressions;
using AmrGrandPrix.API.Models.DTOs.RaceResults;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace AmrGrandPrix.API.Services.FileParser;

/// <summary>
/// Service for parsing PDF race result files
/// Note: PDF parsing is complex and may require manual review
/// </summary>
public class PdfParserService : IFileParserService
{
    private readonly ILogger<PdfParserService> _logger;

    public PdfParserService(ILogger<PdfParserService> logger)
    {
        _logger = logger;
    }

    public bool CanParse(string fileName)
    {
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        return extension == ".pdf";
    }

    public async Task<List<RawResultRow>> ParseAsync(Stream fileStream, string fileName)
    {
        _logger.LogInformation("Parsing PDF file: {FileName}", fileName);

        var rows = new List<RawResultRow>();
        string? currentSection = null;
        List<string>? detectedHeaders = null;

        try
        {
            await Task.Run(() =>
            {
                using var document = PdfDocument.Open(fileStream);
                _logger.LogInformation("PDF has {PageCount} pages", document.NumberOfPages);

                foreach (var page in document.GetPages())
                {
                    var text = page.Text;
                    var lines = text.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

                    _logger.LogDebug("Processing page {PageNumber} with {LineCount} lines", page.Number, lines.Count);

                    foreach (var line in lines)
                    {
                        // Check if this is a section header
                        if (IsSectionHeader(line))
                        {
                            currentSection = line;
                            _logger.LogDebug("Detected section header: {Section}", currentSection);
                            continue;
                        }

                        // Try to detect header row (contains common column names)
                        if (detectedHeaders == null && IsHeaderRow(line))
                        {
                            detectedHeaders = ParseHeaderRow(line);
                            _logger.LogInformation("Detected headers: {Headers}", string.Join(", ", detectedHeaders));
                            continue;
                        }

                        // If we have headers, try to parse as data row
                        if (detectedHeaders != null)
                        {
                            var columns = ParseDataRow(line, detectedHeaders);
                            if (columns != null && columns.Count > 0)
                            {
                                rows.Add(new RawResultRow
                                {
                                    RowNumber = rows.Count + 1,
                                    Columns = columns,
                                    SectionHeader = currentSection
                                });
                            }
                        }
                    }
                }
            });

            _logger.LogInformation("Successfully parsed {Count} rows from PDF", rows.Count);
            return rows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse PDF file: {FileName}", fileName);
            throw new InvalidOperationException($"Failed to parse PDF file: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Detects if a line is a section header
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

        return sectionPatterns.Any(pattern => upperLine.Contains(pattern));
    }

    /// <summary>
    /// Detects if a line is a header row (contains column names)
    /// </summary>
    private bool IsHeaderRow(string line)
    {
        var upperLine = line.ToUpperInvariant();

        var headerKeywords = new[]
        {
            "PLACE", "NAME", "TIME", "AGE", "BIB",
            "RANK", "RUNNER", "FINISH", "GENDER", "SEX"
        };

        // Must contain at least 2 header keywords
        return headerKeywords.Count(keyword => upperLine.Contains(keyword)) >= 2;
    }

    /// <summary>
    /// Parses a header row into column names
    /// </summary>
    private List<string> ParseHeaderRow(string line)
    {
        // Split by multiple spaces (common in PDF tables)
        var parts = Regex.Split(line, @"\s{2,}")
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        return parts;
    }

    /// <summary>
    /// Parses a data row into column values
    /// </summary>
    private Dictionary<string, string>? ParseDataRow(string line, List<string> headers)
    {
        try
        {
            // Split by multiple spaces
            var parts = Regex.Split(line, @"\s{2,}")
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            // Skip if not enough parts
            if (parts.Count < 2)
                return null;

            var columns = new Dictionary<string, string>();

            // Try to intelligently map parts to headers
            // This is a simplified approach - may need adjustment based on actual PDF formats
            for (int i = 0; i < Math.Min(parts.Count, headers.Count); i++)
            {
                columns[headers[i]] = parts[i];
            }

            // Special handling: if we have more parts than headers, concatenate extra parts
            // (often names are split across multiple columns)
            if (parts.Count > headers.Count)
            {
                var extraParts = string.Join(" ", parts.Skip(headers.Count));
                if (columns.ContainsKey("Name") || columns.ContainsKey("NAME") ||
                    columns.ContainsKey("Runner") || columns.ContainsKey("RUNNER"))
                {
                    var nameKey = columns.Keys.FirstOrDefault(k =>
                        k.Equals("Name", StringComparison.OrdinalIgnoreCase) ||
                        k.Equals("Runner", StringComparison.OrdinalIgnoreCase));

                    if (nameKey != null)
                    {
                        columns[nameKey] += " " + extraParts;
                    }
                }
            }

            return columns;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse data row: {Line}", line);
            return null;
        }
    }
}
