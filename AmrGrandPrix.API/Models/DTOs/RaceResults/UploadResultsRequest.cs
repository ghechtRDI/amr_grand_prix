using Microsoft.AspNetCore.Http;

namespace AmrGrandPrix.API.Models.DTOs.RaceResults;

public class UploadResultsRequest
{
    public Guid RaceId { get; set; }
    public IFormFile File { get; set; } = null!;

    /// <summary>
    /// Comma-separated course/variant names already known for this event (e.g. from other race
    /// instances in the same series), used to hint the LLM when this file covers multiple
    /// variants at once. Optional.
    /// </summary>
    public string? KnownVariants { get; set; }
}

public class UploadResultsResponse
{
    public Guid UploadBatchId { get; set; }
    public List<ResultRow> ParsedResults { get; set; } = new();
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int RowsWithIssues { get; set; }
}

public class ResumeBatchResponse
{
    public Guid UploadBatchId { get; set; }
    public Guid RaceId { get; set; }
    public string RaceName { get; set; } = string.Empty;
    public DateOnly RaceDate { get; set; }
    public bool IsGrandPrixRace { get; set; }
    public string? CourseVariant { get; set; }
    public string FileName { get; set; } = string.Empty;
    public List<ResultRow> ParsedResults { get; set; } = new();
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int RowsWithIssues { get; set; }
}

public class ValidateResultsRequest
{
    public Guid UploadBatchId { get; set; }
    public List<ResultRow> Results { get; set; } = new();
}

public class SaveResultsRequest
{
    /// <summary>
    /// The batch to save against. Leave null (and set <see cref="SourceUploadBatchId"/> instead)
    /// when saving an additional course-variant group split out of one upload into a different
    /// race — the server will clone a new batch from the source for audit lineage.
    /// </summary>
    public Guid? UploadBatchId { get; set; }

    /// <summary>
    /// When <see cref="UploadBatchId"/> is null, the original batch to clone audit fields
    /// (raw LLM JSON, model, token counts, file name/type) from for this group's new batch.
    /// </summary>
    public Guid? SourceUploadBatchId { get; set; }

    public Guid RaceId { get; set; }
    public List<ResultRow> Results { get; set; } = new();
}

public class SaveResultsResponse
{
    public bool Success { get; set; }
    public Guid RaceId { get; set; }
    public int ResultsSaved { get; set; }
    public int NewRunnersCreated { get; set; }
    public List<Guid> ResultIds { get; set; } = new();
    public List<SkippedResultDto> SkippedResults { get; set; } = new();
    public string? Message { get; set; }
}

/// <summary>
/// A result row that was not saved, and why.
/// </summary>
public class SkippedResultDto
{
    public int RowNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Fields an admin can edit on an already-saved RaceResult.
/// </summary>
public class UpdateRaceResultRequest
{
    public int? Bib { get; set; }
    public int? Place { get; set; }
    public string? TimeString { get; set; }
    public int? Age { get; set; }
    public string? AgeCategory { get; set; }
    public Gender Gender { get; set; }
    public ResultStatus Status { get; set; }
    public string? Notes { get; set; }
}
