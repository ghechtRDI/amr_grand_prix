using AmrGrandPrix.API.Models;

namespace AmrGrandPrix.API.Services.Runners;

/// <summary>
/// Helpers for a runner's names: the candidates matching compares against, and alternate-name
/// cleanup. See DOCS/RUNNER_MATCHING.md.
/// </summary>
public static class RunnerNames
{
    public const int MaxNameLength = 100;

    /// <summary>
    /// Every full name a runner might appear under, legal name first: legal, each alternate, and
    /// the preferred name, which is also paired with the last name when it's just a first name
    /// (e.g. "Bob" → "Bob Smith"). Cleaned and de-duplicated by <see cref="Normalize"/>.
    /// </summary>
    public static List<string> NameCandidates(Runner runner) =>
        NameCandidates($"{runner.FirstName} {runner.LastName}", runner.LastName, runner.PreferredName, runner.AlternateNames);

    /// <inheritdoc cref="NameCandidates(Runner)"/>
    public static List<string> NameCandidates(string legalName, string? lastName, string? preferredName, IEnumerable<string> alternateNames)
    {
        var names = new List<string?> { legalName };
        names.AddRange(alternateNames);
        if (!string.IsNullOrWhiteSpace(preferredName))
        {
            names.Add(preferredName);
            if (!preferredName.Trim().Contains(' ') && !string.IsNullOrWhiteSpace(lastName))
                names.Add($"{preferredName.Trim()} {lastName.Trim()}");
        }

        return Normalize(names);
    }

    /// <summary>
    /// True when <paramref name="name"/> is already one of the runner's
    /// <see cref="NameCandidates(Runner)"/>, ignoring case and extra whitespace.
    /// </summary>
    public static bool IsKnownName(Runner runner, string? name)
    {
        var cleaned = Normalize([name]).FirstOrDefault();
        return cleaned == null ||
               NameCandidates(runner).Contains(cleaned, StringComparer.OrdinalIgnoreCase);
    }

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
