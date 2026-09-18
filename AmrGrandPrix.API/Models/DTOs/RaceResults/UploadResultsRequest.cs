using Microsoft.AspNetCore.Http;

namespace AmrGrandPrix.API.Models.DTOs.RaceResults;

public class UploadResultsRequest
{
    public Guid RaceId { get; set; }
    public IFormFile File { get; set; } = null!;
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
    public Guid UploadBatchId { get; set; }
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
