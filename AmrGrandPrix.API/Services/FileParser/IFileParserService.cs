using AmrGrandPrix.API.Models.DTOs.RaceResults;

namespace AmrGrandPrix.API.Services.FileParser;

/// <summary>
/// Interface for parsing race result files
/// </summary>
public interface IFileParserService
{
    /// <summary>
    /// Parses a race results file and returns raw result rows
    /// </summary>
    /// <param name="fileStream">The file stream to parse</param>
    /// <param name="fileName">The name of the file (used for type detection)</param>
    /// <returns>List of raw result rows</returns>
    Task<List<RawResultRow>> ParseAsync(Stream fileStream, string fileName);

    /// <summary>
    /// Checks if the service can parse the given file type
    /// </summary>
    /// <param name="fileName">The name of the file</param>
    /// <returns>True if the service can parse this file type</returns>
    bool CanParse(string fileName);
}
