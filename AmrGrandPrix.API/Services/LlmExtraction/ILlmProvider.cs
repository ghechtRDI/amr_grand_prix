namespace AmrGrandPrix.API.Services.LlmExtraction;

public interface ILlmProvider
{
    /// <param name="knownVariants">
    /// Course/variant names already on record for this event (e.g. from other race instances in
    /// the same series), passed as a hint so the model reuses the exact known label for a section
    /// instead of inventing a slightly different one when a file covers multiple variants.
    /// </param>
    Task<LlmProviderResult> ExtractAsync(string text, string fileName, IReadOnlyList<string>? knownVariants = null, CancellationToken ct = default);
}

public record LlmProviderResult(string Json, string Model, int InputTokens, int OutputTokens);
