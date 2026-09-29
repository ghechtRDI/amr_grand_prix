namespace AmrGrandPrix.API.Services.LlmExtraction;

public interface ILlmExtractionService
{
    /// <param name="knownVariants">
    /// Course/variant names already on record for this event, passed through to the LLM provider
    /// as a labeling hint (see <see cref="ILlmProvider.ExtractAsync"/>).
    /// </param>
    /// <param name="onlyTheseVariants">The file covers exactly <paramref name="knownVariants"/> (as chosen by the admin), no others.</param>
    Task<ExtractionResult> ExtractAsync(Stream file, string fileName, IReadOnlyList<string>? knownVariants = null, bool onlyTheseVariants = false, CancellationToken ct = default);

    /// <summary>Rebuild extracted sections from a previously-stored raw LLM JSON response, without calling the LLM again.</summary>
    List<ExtractedSection> RehydrateSections(string rawJson, string fileName);
}
