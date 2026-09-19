using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs.RaceResults;
using AmrGrandPrix.API.Services.LlmExtraction;

namespace AmrGrandPrix.API.Services.ResultsProcessing;

public interface IResultsProcessingService
{
    /// <summary>
    /// Convert LLM-extracted sections into validated ResultRows.
    /// </summary>
    Task<List<ResultRow>> ProcessResultsAsync(List<ExtractedSection> sections);

    /// <summary>Parse a time string into a TimeSpan.</summary>
    TimeSpan? ParseTime(string? timeString);

    /// <summary>Infer gender from a section header string.</summary>
    Gender? DetectGenderFromSection(string? sectionHeader);

    /// <summary>Validate a result row and return any issues.</summary>
    List<ValidationIssue> ValidateRow(ResultRow row);
}
