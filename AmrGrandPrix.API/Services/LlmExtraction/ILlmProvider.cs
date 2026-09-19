namespace AmrGrandPrix.API.Services.LlmExtraction;

public interface ILlmProvider
{
    Task<LlmProviderResult> ExtractAsync(string text, string fileName, CancellationToken ct = default);
}

public record LlmProviderResult(string Json, string Model, int InputTokens, int OutputTokens);
