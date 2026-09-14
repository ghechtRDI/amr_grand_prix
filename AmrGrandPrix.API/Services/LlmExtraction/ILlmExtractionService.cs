namespace AmrGrandPrix.API.Services.LlmExtraction;

public interface ILlmExtractionService
{
    Task<ExtractionResult> ExtractAsync(Stream file, string fileName, CancellationToken ct = default);
}
