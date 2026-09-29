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

    /// <summary>
    /// Links each row whose detected course label matches one of the series' variants (by name or
    /// alias, case-insensitively) to that variant, rewriting the label to the canonical name.
    /// Rows with no label, or a label that matches no variant, are left unlinked.
    /// </summary>
    void AssignVariants(List<ResultRow> rows, IReadOnlyCollection<RaceVariant> seriesVariants);

    /// <summary>Parse a time string into a TimeSpan.</summary>
    TimeSpan? ParseTime(string? timeString);

    /// <summary>Infer gender from a section header string.</summary>
    Gender? DetectGenderFromSection(string? sectionHeader);

    /// <summary>Validate a result row and return any issues.</summary>
    List<ValidationIssue> ValidateRow(ResultRow row);
}
