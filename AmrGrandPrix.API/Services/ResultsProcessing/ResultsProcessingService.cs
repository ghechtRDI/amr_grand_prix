using System.Text.RegularExpressions;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs.RaceResults;
using AmrGrandPrix.API.Services.LlmExtraction;

namespace AmrGrandPrix.API.Services.ResultsProcessing;

public class ResultsProcessingService : IResultsProcessingService
{
    private readonly ILogger<ResultsProcessingService> _logger;

    public ResultsProcessingService(ILogger<ResultsProcessingService> logger)
    {
        _logger = logger;
    }

    public async Task<List<ResultRow>> ProcessResultsAsync(List<ExtractedSection> sections)
    {
        _logger.LogInformation("Processing {SectionCount} extracted section(s)", sections.Count);

        var resultRows = new List<ResultRow>();
        int rowNumber  = 0;

        foreach (var section in sections)
        {
            // Resolve section-level gender fallback
            var sectionGender = ParseGender(section.Gender)
                ?? DetectGenderFromSection(section.Name);

            // Detect batch-wide "Last, First" format as safety net (LLM should convert,
            // but some edge cases may slip through)
            var commaCount = section.Rows.Count(r => r.Name?.Contains(',') == true);
            var useLastFirst = section.Rows.Count > 0 && commaCount * 2 > section.Rows.Count;
            if (useLastFirst)
                _logger.LogInformation("Section '{Section}': detected residual Last,First format", section.Name);

            foreach (var row in section.Rows)
            {
                rowNumber++;
                try
                {
                    var resultRow = ProcessSingleRow(row, sectionGender, useLastFirst, rowNumber);
                    resultRows.Add(resultRow);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing row {RowNumber}, skipping", rowNumber);
                    resultRows.Add(new ResultRow
                    {
                        RowNumber = rowNumber,
                        ValidationIssues = new List<ValidationIssue>
                        {
                            new() { Field = "Row", Severity = ValidationSeverity.Error,
                                    Message = $"Failed to process row: {ex.Message}" }
                        }
                    });
                }
            }
        }

        _logger.LogInformation("Processed {Count} result rows", resultRows.Count);
        return await Task.FromResult(resultRows);
    }

    private ResultRow ProcessSingleRow(
        ExtractedRow row, Gender? sectionGender, bool useLastFirst, int rowNumber)
    {
        var name = row.Name?.Trim() ?? string.Empty;

        // Safety-net "Last, First" → "First Last" conversion
        if (useLastFirst && name.Contains(','))
        {
            var parts = name.Split(',', 2);
            name = $"{parts[1].Trim()} {parts[0].Trim()}";
        }

        var status = ParseStatus(row.Status);

        var resultRow = new ResultRow
        {
            RowNumber   = rowNumber,
            Name        = name,
            Age         = row.Age,
            AgeCategory = row.Age.HasValue ? null : NormalizeAgeCategory(row.AgeCategory),
            Place       = row.Place,
            TimeString  = row.TimeString,
            Time        = ParseTime(row.TimeString),
            Gender      = ParseGender(row.Gender) ?? sectionGender,
            Status      = status,
            Notes       = row.Notes
        };

        resultRow.ValidationIssues = ValidateRow(resultRow);
        return resultRow;
    }

    public TimeSpan? ParseTime(string? timeString)
    {
        if (string.IsNullOrWhiteSpace(timeString))
            return null;

        var trimmed = timeString.Trim().ToUpperInvariant();

        if (trimmed is "DNF" or "DNS" or "DQ" or "N/A" or "NA")
            return null;

        // Strip annotation characters (*, #, †, CR, WR, PR, AR)
        trimmed = Regex.Replace(trimmed, @"[*#†‡]|CR$|WR$|PR$|AR$", "").Trim();
        trimmed = Regex.Replace(trimmed, @"\s+", "");

        // Replace comma-decimal separator ("1:27:12,8" → "1:27:12.8")
        trimmed = Regex.Replace(trimmed, @"(\d+:\d{2}:\d{2}),(\d+)$", "$1.$2");
        trimmed = Regex.Replace(trimmed, @"(\d+:\d{2}),(\d+)$",        "$1.$2");

        if (TimeSpan.TryParse(trimmed, out var ts))
            return ts;

        // H:MM:SS[.fff]
        var m1 = Regex.Match(trimmed, @"^(\d{1,2}):(\d{2}):(\d{2})(?:\.(\d+))?$");
        if (m1.Success)
        {
            var ms = m1.Groups[4].Success
                ? int.Parse(m1.Groups[4].Value.PadRight(3, '0')[..3])
                : 0;
            return new TimeSpan(0, int.Parse(m1.Groups[1].Value),
                                   int.Parse(m1.Groups[2].Value),
                                   int.Parse(m1.Groups[3].Value), ms);
        }

        // MM:SS[.fff]
        var m2 = Regex.Match(trimmed, @"^(\d{1,3}):(\d{2})(?:\.(\d+))?$");
        if (m2.Success)
        {
            var ms = m2.Groups[3].Success
                ? int.Parse(m2.Groups[3].Value.PadRight(3, '0')[..3])
                : 0;
            return new TimeSpan(0, 0, int.Parse(m2.Groups[1].Value),
                                         int.Parse(m2.Groups[2].Value), ms);
        }

        // Bare seconds
        if (int.TryParse(trimmed, out var secs))
            return TimeSpan.FromSeconds(secs);

        _logger.LogWarning("Unable to parse time string: '{TimeString}'", timeString);
        return null;
    }

    public Gender? DetectGenderFromSection(string? sectionHeader)
    {
        if (string.IsNullOrWhiteSpace(sectionHeader))
            return null;

        var upper = sectionHeader.ToUpperInvariant();

        if ((upper.Contains("MALE") && !upper.Contains("FEMALE")) ||
            (upper.Contains("MEN")  && !upper.Contains("WOMEN"))  ||
             upper.Contains("MAN")  ||
             upper.Contains("BOYS"))
            return Gender.Male;

        if (upper.Contains("FEMALE") || upper.Contains("WOMEN") ||
            upper.Contains("WOMAN")  || upper.Contains("GIRLS"))
            return Gender.Female;

        return null;
    }

    public List<ValidationIssue> ValidateRow(ResultRow row)
    {
        var issues = new List<ValidationIssue>();

        if (string.IsNullOrWhiteSpace(row.Name))
            issues.Add(new() { Field = "Name", Severity = ValidationSeverity.Error, Message = "Name is required" });

        if (!row.Age.HasValue && string.IsNullOrEmpty(row.AgeCategory))
            issues.Add(new() { Field = "Age", Severity = ValidationSeverity.Warning, Message = "Age is missing" });
        else if (row.Age is < 5 or > 100)
            issues.Add(new() { Field = "Age", Severity = ValidationSeverity.Warning,
                               Message = $"Age {row.Age} is outside typical range (5-100)" });

        if (!row.Gender.HasValue)
            issues.Add(new() { Field = "Gender", Severity = ValidationSeverity.Warning, Message = "Gender is missing" });

        if (row.Status == ResultStatus.Finished)
        {
            if (!row.Time.HasValue)
                issues.Add(new() { Field = "Time", Severity = ValidationSeverity.Error,
                                   Message = "Finish time is required for completed results" });
            else if (row.Time.Value.TotalHours > 24)
                issues.Add(new() { Field = "Time", Severity = ValidationSeverity.Warning,
                                   Message = "Finish time exceeds 24 hours - please verify" });
        }

        if (row.Status == ResultStatus.Finished && !row.Place.HasValue)
            issues.Add(new() { Field = "Place", Severity = ValidationSeverity.Info,
                               Message = "Place is missing for finished result" });

        return issues;
    }

    private static Gender? ParseGender(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.ToUpperInvariant().Trim() switch
        {
            "M" or "MALE" or "MAN" or "BOY"         => Gender.Male,
            "F" or "FEMALE" or "WOMAN" or "GIRL"    => Gender.Female,
            "NB" or "NONBINARY" or "NON-BINARY" or "X" => Gender.Nonbinary,
            _ => null
        };
    }

    private static string? NormalizeAgeCategory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        var match = GrandPrixConstants.AgeCategories
            .FirstOrDefault(c => string.Equals(c.Name, trimmed, StringComparison.OrdinalIgnoreCase));

        return match?.Name; // null if the LLM produced a category name we don't recognize
    }

    private static ResultStatus ParseStatus(string? value) =>
        value?.ToUpperInvariant().Trim() switch
        {
            "DNF" => ResultStatus.DNF,
            "DNS" => ResultStatus.DNS,
            "DQ"  => ResultStatus.DQ,
            _     => ResultStatus.Finished
        };
}
