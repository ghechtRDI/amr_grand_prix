using AmrGrandPrix.API.Models;

namespace AmrGrandPrix.API.Common;

/// <summary>
/// The span of dates a runner could have been born on, inferred from the ages and age categories
/// reported in their race results. Both bounds are inclusive; a null bound is open-ended, and both
/// null means nothing is known. See DOCS/RUNNER_MATCHING.md ("Estimated birth date range").
/// </summary>
public readonly record struct BirthDateRange(DateOnly? Earliest, DateOnly? Latest)
{
    /// <summary>
    /// The widest range that still counts as an estimate of a birth date: one reported exact age
    /// spans 365-366 days. Wider ranges (age categories only) narrow matching but don't produce
    /// an estimated age or birth year.
    /// </summary>
    public const int MaxEstimateWidthDays = 366;

    public static BirthDateRange Unknown => default;

    public bool IsUnknown => Earliest is null && Latest is null;

    /// <summary>Someone aged <paramref name="age"/> on <paramref name="asOf"/>.</summary>
    public static BirthDateRange FromAge(int age, DateOnly asOf) =>
        FromAgeSpan(age, age, asOf);

    /// <summary>
    /// Someone whose age on <paramref name="asOf"/> was between <paramref name="minAge"/> and
    /// <paramref name="maxAge"/> (inclusive); a null <paramref name="maxAge"/> has no upper limit.
    /// </summary>
    public static BirthDateRange FromAgeSpan(int minAge, int? maxAge, DateOnly asOf) => new(
        maxAge is { } max ? asOf.AddYears(-(max + 1)).AddDays(1) : null,
        asOf.AddYears(-minAge));

    /// <summary>
    /// Someone in age category <paramref name="category"/> (e.g. "40-49") on <paramref name="asOf"/>.
    /// The oldest category is open-ended. Unknown when the category isn't recognized.
    /// </summary>
    public static BirthDateRange FromAgeCategory(string? category, DateOnly asOf)
    {
        var match = GrandPrixConstants.AgeCategories
            .FirstOrDefault(c => string.Equals(c.Name, category?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match == null)
            return Unknown;

        var isOldest = match.MinAge == GrandPrixConstants.AgeCategories.Max(c => c.MinAge);
        return FromAgeSpan(match.MinAge, isOldest ? null : match.MaxAge, asOf);
    }

    /// <summary>What one race result says: its exact age if reported, otherwise its age category.</summary>
    public static BirthDateRange FromResult(int? age, string? ageCategory, DateOnly asOf) =>
        age is { } a ? FromAge(a, asOf) : FromAgeCategory(ageCategory, asOf);

    /// <summary>Born some time in <paramref name="year"/>.</summary>
    public static BirthDateRange ForYear(int year) => new(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));

    /// <summary>A known date of birth.</summary>
    public static BirthDateRange Exact(DateOnly dateOfBirth) => new(dateOfBirth, dateOfBirth);

    /// <summary>The dates in both ranges, or null when they don't overlap.</summary>
    public BirthDateRange? Intersect(BirthDateRange other)
    {
        var earliest = Later(Earliest, other.Earliest);
        var latest = Earlier(Latest, other.Latest);
        if (earliest is { } e && latest is { } l && e > l)
            return null;
        return new BirthDateRange(earliest, latest);
    }

    public bool Overlaps(BirthDateRange other) => Intersect(other) != null;

    /// <summary>Both bounds pushed out by <paramref name="years"/>, for data-entry tolerance.</summary>
    public BirthDateRange Widen(int years) => new(Earliest?.AddYears(-years), Latest?.AddYears(years));

    public bool Contains(DateOnly date) =>
        (Earliest is not { } e || date >= e) && (Latest is not { } l || date <= l);

    /// <summary>True when the range is closed and no wider than <see cref="MaxEstimateWidthDays"/>.</summary>
    public bool IsNarrow =>
        Earliest is { } e && Latest is { } l && l.DayNumber - e.DayNumber <= MaxEstimateWidthDays;

    /// <summary>The middle of a narrow range: the best single guess at the birth date.</summary>
    public DateOnly? EstimatedDateOfBirth =>
        IsNarrow ? Earliest!.Value.AddDays((Latest!.Value.DayNumber - Earliest.Value.DayNumber) / 2) : null;

    public int? EstimatedYear => EstimatedDateOfBirth?.Year;

    /// <summary>
    /// Folds one result's <paramref name="evidence"/> into <paramref name="current"/>: an unknown
    /// side takes the other; otherwise the overlap. When they don't overlap the evidence
    /// contradicts what's known, so <paramref name="current"/> is kept and
    /// <paramref name="conflict"/> is set.
    /// </summary>
    public static BirthDateRange Combine(BirthDateRange current, BirthDateRange evidence, out bool conflict)
    {
        conflict = false;
        if (evidence.IsUnknown)
            return current;
        if (current.IsUnknown)
            return evidence;

        if (current.Intersect(evidence) is { } overlap)
            return overlap;

        conflict = true;
        return current;
    }

    private static DateOnly? Later(DateOnly? a, DateOnly? b) =>
        a is null ? b : b is null ? a : (a > b ? a : b);

    private static DateOnly? Earlier(DateOnly? a, DateOnly? b) =>
        a is null ? b : b is null ? a : (a < b ? a : b);
}
