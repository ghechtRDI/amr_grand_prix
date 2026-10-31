namespace AmrGrandPrix.API.Services.Runners;

/// <summary>
/// Helpers for a runner's alternate-name list.
/// </summary>
public static class RunnerNames
{
    public const int MaxNameLength = 100;

    /// <summary>
    /// Trims each name and collapses internal whitespace, drops blanks, and removes
    /// case-insensitive duplicates, keeping the first spelling seen.
    /// </summary>
    public static List<string> Normalize(IEnumerable<string?> names)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var cleaned = string.Join(" ", name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (cleaned.Length > MaxNameLength)
                cleaned = cleaned[..MaxNameLength];

            if (seen.Add(cleaned))
                result.Add(cleaned);
        }

        return result;
    }
}
