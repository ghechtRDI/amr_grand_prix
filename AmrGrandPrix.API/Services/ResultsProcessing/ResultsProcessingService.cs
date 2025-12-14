using System.Text.RegularExpressions;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs.RaceResults;

namespace AmrGrandPrix.API.Services.ResultsProcessing;

/// <summary>
/// Service for processing and validating parsed race results
/// </summary>
public class ResultsProcessingService : IResultsProcessingService
{
    private readonly ILogger<ResultsProcessingService> _logger;

    // Header mappings for normalization
    private static readonly Dictionary<string, string[]> HeaderMappings = new()
    {
        { "Name", new[] { "name", "runner", "participant", "first name", "firstname", "last name", "lastname", "full name", "fullname", "f name", "l name" } },
        { "Age", new[] { "age", "ag", "years", "yrs" } },
        { "Place", new[] { "place", "position", "rank", "overall", "pl", "pos", "overall place", "finish" } },
        { "Time", new[] { "time", "finish time", "clock time", "chip time", "gun time", "total time", "final time" } },
        { "Gender", new[] { "gender", "sex", "m/f", "male/female", "div", "division" } },
        { "Bib", new[] { "bib", "bib #", "bib#", "number", "bib number", "#" } }
    };

    public ResultsProcessingService(ILogger<ResultsProcessingService> logger)
    {
        _logger = logger;
    }

    public async Task<List<ResultRow>> ProcessResultsAsync(
        List<RawResultRow> rawRows,
        Dictionary<string, string>? columnMappings = null)
    {
        _logger.LogInformation("Processing {Count} raw result rows", rawRows.Count);

        var resultRows = new List<ResultRow>();

        // Detect column mappings from first row if not provided
        if (columnMappings == null && rawRows.Any())
        {
            var headers = rawRows.First().Columns.Keys.ToArray();
            columnMappings = NormalizeHeaders(headers);
            _logger.LogInformation("Auto-detected column mappings: {Mappings}",
                string.Join(", ", columnMappings.Select(kvp => $"{kvp.Key}→{kvp.Value}")));
        }

        foreach (var rawRow in rawRows)
        {
            try
            {
                var resultRow = ProcessSingleRow(rawRow, columnMappings ?? new Dictionary<string, string>());
                resultRows.Add(resultRow);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing row {RowNumber}, skipping", rawRow.RowNumber);

                // Add error row with validation issue
                resultRows.Add(new ResultRow
                {
                    RowNumber = rawRow.RowNumber,
                    ValidationIssues = new List<ValidationIssue>
                    {
                        new ValidationIssue
                        {
                            Field = "Row",
                            Severity = ValidationSeverity.Error,
                            Message = $"Failed to process row: {ex.Message}"
                        }
                    }
                });
            }
        }

        _logger.LogInformation("Successfully processed {Count} result rows", resultRows.Count);
        return await Task.FromResult(resultRows);
    }

    private ResultRow ProcessSingleRow(RawResultRow rawRow, Dictionary<string, string> columnMappings)
    {
        var resultRow = new ResultRow
        {
            RowNumber = rawRow.RowNumber
        };

        // Extract fields using column mappings
        resultRow.Name = GetMappedValue(rawRow.Columns, columnMappings, "Name")?.Trim() ?? string.Empty;

        var ageString = GetMappedValue(rawRow.Columns, columnMappings, "Age");
        resultRow.Age = int.TryParse(ageString, out var age) ? age : null;

        var placeString = GetMappedValue(rawRow.Columns, columnMappings, "Place");
        resultRow.Place = int.TryParse(placeString, out var place) ? place : null;

        var bibString = GetMappedValue(rawRow.Columns, columnMappings, "Bib");
        resultRow.Bib = int.TryParse(bibString, out var bib) ? bib : null;

        var timeString = GetMappedValue(rawRow.Columns, columnMappings, "Time");
        resultRow.TimeString = timeString;
        resultRow.Time = ParseTime(timeString);

        // Determine result status
        if (timeString != null)
        {
            var upperTime = timeString.ToUpperInvariant().Trim();
            resultRow.Status = upperTime switch
            {
                "DNF" => ResultStatus.DNF,
                "DNS" => ResultStatus.DNS,
                "DQ" => ResultStatus.DQ,
                _ => ResultStatus.Finished
            };
        }

        // Gender detection: first try column, then section header
        var genderString = GetMappedValue(rawRow.Columns, columnMappings, "Gender");
        if (!string.IsNullOrWhiteSpace(genderString))
        {
            resultRow.Gender = ParseGender(genderString);
        }
        else if (!string.IsNullOrWhiteSpace(rawRow.SectionHeader))
        {
            resultRow.Gender = DetectGenderFromSection(rawRow.SectionHeader);
        }

        // Validate the row
        resultRow.ValidationIssues = ValidateRow(resultRow);

        return resultRow;
    }

    private string? GetMappedValue(Dictionary<string, string> columns, Dictionary<string, string> mappings, string standardField)
    {
        // Find the source column that maps to this standard field
        var sourceColumn = mappings.FirstOrDefault(kvp => kvp.Value == standardField).Key;

        if (sourceColumn != null && columns.TryGetValue(sourceColumn, out var value))
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        return null;
    }

    public Dictionary<string, string> NormalizeHeaders(string[] headers)
    {
        var normalized = new Dictionary<string, string>();

        foreach (var header in headers)
        {
            var cleanHeader = header.Trim();
            var standardField = FindStandardField(cleanHeader);

            if (standardField != null)
            {
                normalized[cleanHeader] = standardField;
                _logger.LogDebug("Mapped header '{Header}' to '{Standard}'", cleanHeader, standardField);
            }
            else
            {
                _logger.LogDebug("No mapping found for header '{Header}'", cleanHeader);
            }
        }

        return normalized;
    }

    private string? FindStandardField(string header)
    {
        var lowerHeader = header.ToLowerInvariant().Trim();

        foreach (var mapping in HeaderMappings)
        {
            if (mapping.Value.Contains(lowerHeader))
            {
                return mapping.Key;
            }
        }

        // Check for partial matches (e.g., "finish time" contains "time")
        foreach (var mapping in HeaderMappings)
        {
            if (mapping.Value.Any(pattern => lowerHeader.Contains(pattern) || pattern.Contains(lowerHeader)))
            {
                return mapping.Key;
            }
        }

        return null;
    }

    public TimeSpan? ParseTime(string? timeString)
    {
        if (string.IsNullOrWhiteSpace(timeString))
            return null;

        var trimmed = timeString.Trim().ToUpperInvariant();

        // Handle DNF/DNS/DQ
        if (trimmed is "DNF" or "DNS" or "DQ" or "N/A" or "NA")
            return null;

        // Remove any extra whitespace
        trimmed = Regex.Replace(trimmed, @"\s+", "");

        // Try standard TimeSpan.Parse first
        if (TimeSpan.TryParse(trimmed, out var timespan))
            return timespan;

        // Pattern 1: HH:MM:SS or H:MM:SS with optional milliseconds
        var pattern1 = @"^(\d{1,2}):(\d{2}):(\d{2})(?:\.(\d+))?$";
        var match1 = Regex.Match(trimmed, pattern1);
        if (match1.Success)
        {
            var hours = int.Parse(match1.Groups[1].Value);
            var minutes = int.Parse(match1.Groups[2].Value);
            var seconds = int.Parse(match1.Groups[3].Value);
            var milliseconds = 0;

            if (match1.Groups[4].Success)
            {
                var msString = match1.Groups[4].Value.PadRight(3, '0').Substring(0, 3);
                milliseconds = int.Parse(msString);
            }

            return new TimeSpan(0, hours, minutes, seconds, milliseconds);
        }

        // Pattern 2: MM:SS or M:SS with optional milliseconds
        var pattern2 = @"^(\d{1,3}):(\d{2})(?:\.(\d+))?$";
        var match2 = Regex.Match(trimmed, pattern2);
        if (match2.Success)
        {
            var minutes = int.Parse(match2.Groups[1].Value);
            var seconds = int.Parse(match2.Groups[2].Value);
            var milliseconds = 0;

            if (match2.Groups[3].Success)
            {
                var msString = match2.Groups[3].Value.PadRight(3, '0').Substring(0, 3);
                milliseconds = int.Parse(msString);
            }

            return new TimeSpan(0, 0, minutes, seconds, milliseconds);
        }

        // Pattern 3: Seconds only (e.g., "3600" = 1 hour)
        if (int.TryParse(trimmed, out var totalSeconds))
        {
            return TimeSpan.FromSeconds(totalSeconds);
        }

        _logger.LogWarning("Unable to parse time string: '{TimeString}'", timeString);
        return null;
    }

    public Gender? DetectGenderFromSection(string? sectionHeader)
    {
        if (string.IsNullOrWhiteSpace(sectionHeader))
            return null;

        var upper = sectionHeader.ToUpperInvariant();

        // Male patterns
        if (upper.Contains("MALE") && !upper.Contains("FEMALE") ||
            upper.Contains("MEN") && !upper.Contains("WOMEN") ||
            upper.Contains("MAN") ||
            upper.Contains("BOYS"))
        {
            return Gender.Male;
        }

        // Female patterns
        if (upper.Contains("FEMALE") ||
            upper.Contains("WOMEN") ||
            upper.Contains("WOMAN") ||
            upper.Contains("GIRLS"))
        {
            return Gender.Female;
        }

        return null;
    }

    private Gender? ParseGender(string genderString)
    {
        if (string.IsNullOrWhiteSpace(genderString))
            return null;

        var upper = genderString.ToUpperInvariant().Trim();

        return upper switch
        {
            "M" or "MALE" or "MAN" or "BOY" => Gender.Male,
            "F" or "FEMALE" or "WOMAN" or "GIRL" => Gender.Female,
            "NB" or "NONBINARY" or "NON-BINARY" or "X" => Gender.Nonbinary,
            _ => null
        };
    }

    public List<ValidationIssue> ValidateRow(ResultRow row)
    {
        var issues = new List<ValidationIssue>();

        // Required: Name
        if (string.IsNullOrWhiteSpace(row.Name))
        {
            issues.Add(new ValidationIssue
            {
                Field = "Name",
                Severity = ValidationSeverity.Error,
                Message = "Name is required"
            });
        }

        // Required: Age (warn if missing)
        if (!row.Age.HasValue)
        {
            issues.Add(new ValidationIssue
            {
                Field = "Age",
                Severity = ValidationSeverity.Warning,
                Message = "Age is missing"
            });
        }
        else if (row.Age < 5 || row.Age > 100)
        {
            issues.Add(new ValidationIssue
            {
                Field = "Age",
                Severity = ValidationSeverity.Warning,
                Message = $"Age {row.Age} is outside typical range (5-100)"
            });
        }

        // Required: Gender (warn if missing)
        if (!row.Gender.HasValue)
        {
            issues.Add(new ValidationIssue
            {
                Field = "Gender",
                Severity = ValidationSeverity.Warning,
                Message = "Gender is missing"
            });
        }

        // Time validation
        if (row.Status == ResultStatus.Finished)
        {
            if (!row.Time.HasValue)
            {
                issues.Add(new ValidationIssue
                {
                    Field = "Time",
                    Severity = ValidationSeverity.Error,
                    Message = "Finish time is required for completed results"
                });
            }
            else if (row.Time.Value.TotalHours > 24)
            {
                issues.Add(new ValidationIssue
                {
                    Field = "Time",
                    Severity = ValidationSeverity.Warning,
                    Message = "Finish time exceeds 24 hours - please verify"
                });
            }
        }

        // Place validation
        if (row.Status == ResultStatus.Finished && !row.Place.HasValue)
        {
            issues.Add(new ValidationIssue
            {
                Field = "Place",
                Severity = ValidationSeverity.Info,
                Message = "Place is missing for finished result"
            });
        }

        return issues;
    }
}
