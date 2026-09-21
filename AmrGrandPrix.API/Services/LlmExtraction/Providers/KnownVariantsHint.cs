namespace AmrGrandPrix.API.Services.LlmExtraction.Providers;

/// <summary>
/// Builds the shared user-message prefix that hints the LLM toward reusing known course/variant
/// names instead of inventing new labels for a section. Shared by both providers so the wording
/// (and its effect on extraction) stays identical regardless of which one is configured.
/// </summary>
public static class KnownVariantsHint
{
    public static string BuildPrefix(IReadOnlyList<string>? knownVariants)
    {
        if (knownVariants == null || knownVariants.Count == 0)
            return string.Empty;

        var list = string.Join("\n", knownVariants.Select(v => $"- {v}"));
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
