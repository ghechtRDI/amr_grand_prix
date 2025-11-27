namespace AmrGrandPrix.API.Models.DTOs.RaceResults;

/// <summary>
/// Represents a raw row of data parsed from an uploaded file
/// </summary>
public class RawResultRow
{
    /// <summary>
    /// Dictionary of column names to values
    /// </summary>
    public Dictionary<string, string> Columns { get; set; } = new();

    /// <summary>
    /// Row number in the source file
    /// </summary>
    public int RowNumber { get; set; }

    /// <summary>
    /// Section header (e.g., "MALE RESULTS", "FEMALE") for gender detection
    /// </summary>
    public string? SectionHeader { get; set; }
}
