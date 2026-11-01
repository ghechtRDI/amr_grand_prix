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

    public async Task<string> ExtractTextAsync(Stream file, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var text = ext switch
        {
            ".pdf"          => PdfTextExtractor.Extract(file),
            ".csv" or ".txt" => await CsvTextExtractor.ExtractAsync(file),
            ".xlsx" or ".xls" => XlsxTextExtractor.Extract(file),
            _ => throw new NotSupportedException($"File type '{ext}' is not supported")
        };
        _logger.LogInformation("Extracted {Chars} characters from {FileName}", text.Length, fileName);
        return text;
    }

    public async Task<ExtractionResult> ExtractFromTextAsync(string text, string fileName, IReadOnlyList<string>? knownVariants = null, bool onlyTheseVariants = false, Action<int>? onRowsExtracted = null, CancellationToken ct = default)
    {
        var providerResult = await _provider.ExtractAsync(text, fileName, knownVariants, onlyTheseVariants, onRowsExtracted, ct);
        var sections       = RehydrateSections(providerResult.Json, fileName);

        return new ExtractionResult(
            sections,
            providerResult.Json,
            providerResult.Model,
            providerResult.InputTokens,
            providerResult.OutputTokens);
    }

    public List<ExtractedSection> RehydrateSections(string rawJson, string fileName)
    {
        var extraction = DeserializeExtraction(rawJson, fileName);
        return extraction.Sections.Select(s => s.ToExtractedSection()).ToList();
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
    private sealed class LlmExtractionPayload
    {
        public List<LlmSection> Sections { get; set; } = new();
    }

    private sealed class LlmSection
    {
        public string? Name   { get; set; }
        public string? Gender { get; set; }
        public string? Course { get; set; }
        public List<LlmRow> Rows { get; set; } = new();

        public ExtractedSection ToExtractedSection() =>
            new(Name, Gender, Course, Rows.Select(r => r.ToExtractedRow()).ToList());
    }

    private sealed class LlmRow
    {
        public int?    Place       { get; set; }
        public string  Name        { get; set; } = string.Empty;
        public int?    Age         { get; set; }
        [JsonPropertyName("age_category")]
        public string? AgeCategory { get; set; }
        public string? Gender      { get; set; }
        [JsonPropertyName("time_string")]
        public string? TimeString  { get; set; }
        public string? Status      { get; set; }
        public string? Notes       { get; set; }

        public ExtractedRow ToExtractedRow() =>
            new(Place, Name, Age, AgeCategory, Gender, TimeString, Status ?? "Finished", Notes);
    }
}
