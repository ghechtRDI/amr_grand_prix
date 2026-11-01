namespace AmrGrandPrix.API.Services.LlmExtraction;

public interface ILlmExtractionService
{
    /// <summary>Pull the plain text out of an uploaded PDF / CSV / Excel file (fast, no LLM call).</summary>
    /// <exception cref="NotSupportedException">The file type isn't supported.</exception>
    Task<string> ExtractTextAsync(Stream file, string fileName);

    /// <summary>Send previously extracted file text to the LLM and parse its structured results.</summary>
    /// <param name="knownVariants">
    /// Course/variant names already on record for this event, passed through to the LLM provider
    /// as a labeling hint (see <see cref="ILlmProvider.ExtractAsync"/>).
    /// </param>
    /// <param name="onlyTheseVariants">The file covers exactly <paramref name="knownVariants"/> (as chosen by the admin), no others.</param>
    /// <param name="onRowsExtracted">Live count of rows written so far (see <see cref="ILlmProvider.ExtractAsync"/>).</param>
    Task<ExtractionResult> ExtractFromTextAsync(string text, string fileName, IReadOnlyList<string>? knownVariants = null, bool onlyTheseVariants = false, Action<int>? onRowsExtracted = null, CancellationToken ct = default);

    /// <summary>Rebuild extracted sections from a previously-stored raw LLM JSON response, without calling the LLM again.</summary>
    List<ExtractedSection> RehydrateSections(string rawJson, string fileName);
}
