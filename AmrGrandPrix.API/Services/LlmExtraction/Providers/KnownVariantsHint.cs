namespace AmrGrandPrix.API.Services.LlmExtraction.Providers;

/// <summary>
/// Builds the shared user-message prefix that hints the LLM toward reusing known course/variant
/// names instead of inventing new labels for a section. Shared by both providers so the wording
/// (and its effect on extraction) stays identical regardless of which one is configured.
/// </summary>
public static class KnownVariantsHint
{
    /// <param name="onlyTheseVariants">
    /// The admin selected exactly which variants this file contains, so the list is complete: the
    /// model is told to label every section with one of these names and not to spread one set of
    /// results across several variants.
    /// </param>
    public static string BuildPrefix(IReadOnlyList<string>? knownVariants, bool onlyTheseVariants = false)
    {
        if (knownVariants == null || knownVariants.Count == 0)
            return string.Empty;

        var list = string.Join("\n", knownVariants.Select(v => $"- {v}"));

        if (onlyTheseVariants)
            return $"""
                This file contains results for ONLY the following course variants of this event:
                {list}
                Set every section's "course" field to exactly one of these names (whichever
                variant that section's results belong to), even when the file uses a different or
                abbreviated heading for it. Split a variant into several sections (e.g. by gender)
                as usual, but give each the same "course" name. Each result belongs to exactly one
                variant: never repeat the same set of results under more than one course name.
                Do not use any course name that is not on this list.
                ---

                """;

        return $"""
            Known course/variant names already on record for this event:
            {list}
            When this file covers multiple variants, reuse one of these exact names for a
            section's "course" field whenever it matches. Only invent a new label if none of
            these fit.
            ---

            """;
    }
}
