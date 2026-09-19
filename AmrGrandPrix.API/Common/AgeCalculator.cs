using AmrGrandPrix.API.Models;

namespace AmrGrandPrix.API.Common;

/// <summary>
/// Single source of truth for turning a date of birth (or an estimated birth year) into an age.
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
    /// Approximate age as of <paramref name="asOf"/> when only a birth year is known. Without a
    /// month/day this is inherently accurate to within about a year (it assumes the birthday has
    /// already occurred in the reference year).
    /// </summary>
    public static int CalculateAgeFromBirthYear(int birthYear, DateOnly asOf) => asOf.Year - birthYear;

    /// <summary>
    /// A runner's age as of <paramref name="asOf"/>, preferring a verified <see cref="Runner.DateOfBirth"/>
    /// and falling back to <see cref="Runner.EstimatedBirthYear"/>. Null when neither is known.
    /// </summary>
    public static int? GetRunnerAge(Runner runner, DateOnly asOf)
    {
        if (runner.DateOfBirth.HasValue)
            return CalculateAge(runner.DateOfBirth.Value, asOf);

        if (runner.EstimatedBirthYear.HasValue)
            return CalculateAgeFromBirthYear(runner.EstimatedBirthYear.Value, asOf);

        return null;
    }

    /// <summary>
    /// True when the runner's age can only be approximated (no verified date of birth on file).
    /// </summary>
    public static bool IsAgeEstimated(Runner runner) =>
        !runner.DateOfBirth.HasValue && runner.EstimatedBirthYear.HasValue;
}
