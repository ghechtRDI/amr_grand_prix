using AmrGrandPrix.API.Models;

namespace AmrGrandPrix.API.Common;

/// <summary>
/// Single source of truth for turning a date of birth (or an estimated birth date range) into an age.
/// </summary>
public static class AgeCalculator
{
    /// <summary>
    /// Exact age as of <paramref name="asOf"/>, given a known date of birth.
    /// </summary>
    public static int CalculateAge(DateOnly dateOfBirth, DateOnly asOf)
    {
        var age = asOf.Year - dateOfBirth.Year;
        if (dateOfBirth > asOf.AddYears(-age))
            age--;

        return age;
    }

    /// <summary>
    /// What's known about a runner's birth date: their verified <see cref="Runner.DateOfBirth"/> as
    /// a one-day range if set, otherwise <see cref="Runner.EstimatedBirthRange"/>.
    /// </summary>
    public static BirthDateRange GetBirthRange(Runner runner) =>
        runner.DateOfBirth is { } dob ? BirthDateRange.Exact(dob) : runner.EstimatedBirthRange;

    /// <summary>
    /// A runner's age as of <paramref name="asOf"/>, preferring a verified <see cref="Runner.DateOfBirth"/>
    /// and falling back to the middle of a narrow <see cref="Runner.EstimatedBirthRange"/>. Null when
    /// neither is known (including when only age categories are known).
    /// </summary>
    public static int? GetRunnerAge(Runner runner, DateOnly asOf)
    {
        if (runner.DateOfBirth is { } dob)
            return CalculateAge(dob, asOf);

        if (runner.EstimatedBirthRange.EstimatedDateOfBirth is { } estimate)
            return CalculateAge(estimate, asOf);

        return null;
    }

    /// <summary>
    /// The youngest and oldest the runner could be on <paramref name="asOf"/>. Equal for a verified
    /// date of birth; either side null when that bound is unknown.
    /// </summary>
    public static (int? Min, int? Max) GetRunnerAgeRange(Runner runner, DateOnly asOf) =>
        GetAgeRange(GetBirthRange(runner), asOf);

    /// <inheritdoc cref="GetRunnerAgeRange"/>
    public static (int? Min, int? Max) GetAgeRange(BirthDateRange range, DateOnly asOf) => (
        range.Latest is { } latest ? CalculateAge(latest, asOf) : null,
        range.Earliest is { } earliest ? CalculateAge(earliest, asOf) : null);

    /// <summary>
    /// True when the runner's age can only be approximated (no verified date of birth on file).
    /// </summary>
    public static bool IsAgeEstimated(Runner runner) =>
        !runner.DateOfBirth.HasValue && runner.EstimatedBirthRange.IsNarrow;
}
