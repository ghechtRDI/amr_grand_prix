using Microsoft.AspNetCore.Http;

namespace AmrGrandPrix.API.Models.DTOs.RaceResults;

/// <summary>
/// Request for uploading race results file
/// </summary>
public class UploadResultsRequest
{
    public Guid RaceId { get; set; }
    public IFormFile File { get; set; } = null!;
}

/// <summary>
/// Response from uploading and parsing results file
/// </summary>
public class UploadResultsResponse
{
    public Guid UploadBatchId { get; set; }
    public List<ResultRow> ParsedResults { get; set; } = new();
    public List<string> DetectedColumns { get; set; } = new();
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int RowsWithIssues { get; set; }
}

/// <summary>
/// Request for validating corrected results
/// </summary>
public class ValidateResultsRequest
{
    public Guid UploadBatchId { get; set; }
    public List<ResultRow> Results { get; set; } = new();
}

/// <summary>
/// Request for saving validated results
/// </summary>
public class SaveResultsRequest
{
    public Guid UploadBatchId { get; set; }
    public Guid RaceId { get; set; }
    public List<ResultRow> Results { get; set; } = new();
}

/// <summary>
/// Response from saving results
/// </summary>
public class SaveResultsResponse
{
    public bool Success { get; set; }
    public int ResultsSaved { get; set; }
    public int NewRunnersCreated { get; set; }
    public List<Guid> ResultIds { get; set; } = new();
    public string? Message { get; set; }
}
