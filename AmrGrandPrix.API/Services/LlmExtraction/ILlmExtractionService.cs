namespace AmrGrandPrix.API.Services.LlmExtraction;

public interface ILlmExtractionService
{
    Task<ExtractionResult> ExtractAsync(Stream file, string fileName, CancellationToken ct = default);

    /// <summary>Rebuild extracted sections from a previously-stored raw LLM JSON response, without calling the LLM again.</summary>
    List<ExtractedSection> RehydrateSections(string rawJson, string fileName);
}
