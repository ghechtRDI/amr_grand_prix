using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs.RaceResults;

namespace AmrGrandPrix.API.Services.ResultsProcessing;

/// <summary>
/// Service for processing and validating parsed race results
/// </summary>
public interface IResultsProcessingService
{
    /// <summary>
    /// Process raw result rows into validated result rows
    /// </summary>
    /// <param name="rawRows">Raw parsed data from file</param>
    /// <param name="columnMappings">Optional manual column mappings</param>
    /// <returns>Processed and validated result rows</returns>
    Task<List<ResultRow>> ProcessResultsAsync(
        List<RawResultRow> rawRows,
        Dictionary<string, string>? columnMappings = null);

    /// <summary>
    /// Normalize column headers to standard field names
    /// </summary>
    /// <param name="headers">Column headers from file</param>
    /// <returns>Dictionary mapping file headers to standard field names</returns>
    Dictionary<string, string> NormalizeHeaders(string[] headers);

    /// <summary>
    /// Parse a time string into TimeSpan
    /// </summary>
    /// <param name="timeString">Time string (e.g., "1:23:45", "23:45", "DNF")</param>
    /// <returns>Parsed TimeSpan or null if invalid/DNF/DNS/DQ</returns>
    TimeSpan? ParseTime(string timeString);

    /// <summary>
    /// Detect gender from section header
    /// </summary>
    /// <param name="sectionHeader">Section header text (e.g., "MALE RESULTS")</param>
    /// <returns>Detected gender or null</returns>
    Gender? DetectGenderFromSection(string? sectionHeader);

    /// <summary>
    /// Validate a result row and add validation issues
    /// </summary>
    /// <param name="row">Result row to validate</param>
    /// <returns>List of validation issues</returns>
    List<ValidationIssue> ValidateRow(ResultRow row);
}
