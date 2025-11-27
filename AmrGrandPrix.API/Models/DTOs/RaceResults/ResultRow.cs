namespace AmrGrandPrix.API.Models.DTOs.RaceResults;

/// <summary>
/// Represents a processed result row with validation
/// </summary>
public class ResultRow
{
    public int RowNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? Age { get; set; }
    public int? Place { get; set; }
    public string? TimeString { get; set; }
    public TimeSpan? Time { get; set; }
    public Gender? Gender { get; set; }
    public int? Bib { get; set; }
    public ResultStatus Status { get; set; } = ResultStatus.Finished;
    public string? Notes { get; set; }

    /// <summary>
    /// Validation issues found for this row
    /// </summary>
    public List<ValidationIssue> ValidationIssues { get; set; } = new();

    /// <summary>
    /// Possible runner matches from existing database
    /// </summary>
    public List<RunnerMatchDto>? RunnerMatches { get; set; }

    /// <summary>
    /// Selected runner ID (if matched to existing runner)
    /// </summary>
    public Guid? MatchedRunnerId { get; set; }
}

/// <summary>
/// Represents a validation issue with a result row
/// </summary>
public class ValidationIssue
{
    public string Field { get; set; } = string.Empty;
    public ValidationSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Severity levels for validation issues
/// </summary>
public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}
