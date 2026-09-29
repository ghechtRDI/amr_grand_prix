using AmrGrandPrix.API.Models;

namespace AmrGrandPrix.API.Common;

/// <summary>
/// Converts a finish time into an "age-graded" time: the equivalent time for the same effort at
/// open-age peak. Graded time = actual time × age factor.
///
/// The factors are an approximation modeled on the shape of the WMA road-running age-factor curves
/// (performance holds through the mid-30s, then declines at an accelerating rate, somewhat faster
/// for women). They are not the official WMA tables — those are distance-specific, and races here
/// have no distance field and are mostly mountain courses the tables don't cover. Swap the anchors
/// below if an official table is adopted.
/// </summary>
public static class AgeGrading
{
    /// <summary>Age-graded times are only reported for results at this age and above.</summary>
    public const int MinimumAge = 45;

    // (age, factor) anchors; factors are linearly interpolated between them and clamped at the ends.
    private static readonly (int Age, double Factor)[] MaleFactors =
    [
        (35, 1.000), (40, 0.965), (45, 0.930), (50, 0.895), (55, 0.855), (60, 0.815),
        (65, 0.770), (70, 0.720), (75, 0.665), (80, 0.600), (85, 0.530), (90, 0.450), (95, 0.370)
    ];

    private static readonly (int Age, double Factor)[] FemaleFactors =
    [
        (35, 1.000), (40, 0.960), (45, 0.915), (50, 0.870), (55, 0.825), (60, 0.775),
        (65, 0.720), (70, 0.665), (75, 0.605), (80, 0.540), (85, 0.470), (90, 0.395), (95, 0.320)
    ];

    public static double GetFactor(int age, Gender gender) => gender switch
    {
        Gender.Male => Interpolate(MaleFactors, age),
        Gender.Female => Interpolate(FemaleFactors, age),
        // No published curve for nonbinary runners; use the midpoint of the two.
        _ => (Interpolate(MaleFactors, age) + Interpolate(FemaleFactors, age)) / 2
    };

    /// <summary>
    /// The age-graded equivalent of <paramref name="time"/>, or null when the age is unknown or
    /// below <see cref="MinimumAge"/>.
    /// </summary>
    public static TimeSpan? GradeTime(TimeSpan time, int? age, Gender gender)
    {
        if (!age.HasValue || age.Value < MinimumAge)
            return null;

        var seconds = Math.Round(time.TotalSeconds * GetFactor(age.Value, gender));
        return TimeSpan.FromSeconds(seconds);
    }

    private static double Interpolate((int Age, double Factor)[] anchors, int age)
    {
        if (age <= anchors[0].Age) return anchors[0].Factor;
        if (age >= anchors[^1].Age) return anchors[^1].Factor;

        for (var i = 1; i < anchors.Length; i++)
        {
            if (age > anchors[i].Age) continue;
            var (loAge, loFactor) = anchors[i - 1];
            var (hiAge, hiFactor) = anchors[i];
            return loFactor + (hiFactor - loFactor) * (age - loAge) / (hiAge - loAge);
        }

        return anchors[^1].Factor;
    }
}
