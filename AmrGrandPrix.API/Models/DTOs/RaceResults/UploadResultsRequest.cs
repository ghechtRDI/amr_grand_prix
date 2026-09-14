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
    public int ResultsSaved { get; set; }
    public int NewRunnersCreated { get; set; }
    public List<Guid> ResultIds { get; set; } = new();
    public string? Message { get; set; }
}
