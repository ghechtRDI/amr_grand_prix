namespace AmrGrandPrix.API.Services.FileParser;

/// <summary>
/// Factory for selecting the appropriate file parser based on file type
/// </summary>
public class FileParserFactory
{
    private readonly IEnumerable<IFileParserService> _parsers;
    private readonly ILogger<FileParserFactory> _logger;

    public FileParserFactory(
        IEnumerable<IFileParserService> parsers,
        ILogger<FileParserFactory> logger)
    {
        _parsers = parsers;
        _logger = logger;
    }

    /// <summary>
    /// Gets the appropriate parser for the given file
    /// </summary>
    /// <param name="fileName">The name of the file to parse</param>
    /// <returns>The parser that can handle this file type</returns>
    /// <exception cref="NotSupportedException">If no parser can handle this file type</exception>
    public IFileParserService GetParser(string fileName)
    {
        _logger.LogDebug("Finding parser for file: {FileName}", fileName);

        var parser = _parsers.FirstOrDefault(p => p.CanParse(fileName));

        if (parser == null)
        {
            var extension = Path.GetExtension(fileName);
            _logger.LogError("No parser found for file type: {Extension}", extension);
            throw new NotSupportedException($"File type '{extension}' is not supported. Supported types: .csv, .xlsx, .xls, .pdf");
        }

        _logger.LogDebug("Using parser: {ParserType}", parser.GetType().Name);
        return parser;
    }

    /// <summary>
    /// Checks if a file type is supported
    /// </summary>
    public bool IsSupported(string fileName)
    {
        return _parsers.Any(p => p.CanParse(fileName));
    }

    /// <summary>
    /// Gets a list of all supported file extensions
    /// </summary>
    public IEnumerable<string> GetSupportedExtensions()
    {
        return new[] { ".csv", ".txt", ".xlsx", ".xls", ".pdf" };
    }
}
