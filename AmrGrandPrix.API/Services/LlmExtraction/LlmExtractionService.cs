using System.Text.Json;
using System.Text.Json.Serialization;
using AmrGrandPrix.API.Services.LlmExtraction.TextExtractors;

namespace AmrGrandPrix.API.Services.LlmExtraction;

public class LlmExtractionService : ILlmExtractionService
{
    private readonly ILlmProvider _provider;
    private readonly ILogger<LlmExtractionService> _logger;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public LlmExtractionService(ILlmProvider provider, ILogger<LlmExtractionService> logger)
    {
        _provider = provider;
        _logger   = logger;
    }

    public async Task<ExtractionResult> ExtractAsync(Stream file, string fileName, CancellationToken ct = default)
    {
        _logger.LogInformation("Extracting text from {FileName}", fileName);
        var text = await ExtractTextAsync(file, fileName);
        _logger.LogInformation("Extracted {Chars} characters from {FileName}", text.Length, fileName);

        var providerResult = await _provider.ExtractAsync(text, fileName, ct);

        var extraction = DeserializeExtraction(providerResult.Json, fileName);
        var sections   = extraction.Sections.Select(s => s.ToExtractedSection()).ToList();

        return new ExtractionResult(
            sections,
            providerResult.Json,
            providerResult.Model,
            providerResult.InputTokens,
            providerResult.OutputTokens);
    }

    private static async Task<string> ExtractTextAsync(Stream stream, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf"          => PdfTextExtractor.Extract(stream),
            ".csv" or ".txt" => await CsvTextExtractor.ExtractAsync(stream),
            ".xlsx" or ".xls" => XlsxTextExtractor.Extract(stream),
            _ => throw new NotSupportedException($"File type '{ext}' is not supported")
        };
    }

    private LlmExtractionPayload DeserializeExtraction(string json, string fileName)
    {
        try
        {
            var result = JsonSerializer.Deserialize<LlmExtractionPayload>(json, _jsonOpts);
            if (result?.Sections == null)
                throw new InvalidOperationException("LLM returned JSON with no 'sections' field");
            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse LLM JSON for {FileName}: {Json}", fileName, json);
            throw new InvalidOperationException($"LLM returned invalid JSON: {ex.Message}", ex);
        }
    }

    // Internal DTO for deserialization (uses snake_case via JsonPropertyName)
    private class LlmExtractionPayload
    {
        public List<LlmSection> Sections { get; set; } = new();
    }

    private class LlmSection
    {
        public string? Name   { get; set; }
        public string? Gender { get; set; }
        public List<LlmRow> Rows { get; set; } = new();

        public ExtractedSection ToExtractedSection() =>
            new(Name, Gender, Rows.Select(r => r.ToExtractedRow()).ToList());
    }

    private class LlmRow
    {
        public int?    Place       { get; set; }
        public string  Name        { get; set; } = string.Empty;
        public int?    Age         { get; set; }
        public string? Gender      { get; set; }
        [JsonPropertyName("time_string")]
        public string? TimeString  { get; set; }
        public string? Status      { get; set; }
        public string? Notes       { get; set; }

        public ExtractedRow ToExtractedRow() =>
            new(Place, Name, Age, Gender, TimeString, Status ?? "Finished", Notes);
    }
}
