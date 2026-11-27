using AmrGrandPrix.API.Common;

namespace AmrGrandPrix.API.Services.Seo;

/// <summary>
/// Which runner pages search engines may index. Runners under 18 are kept out (noindex, left out
/// of the sitemap). Without a verified date of birth, the runner must be 18+ even at the youngest
/// their estimated birth date range allows; no upper bound on the birth date (unknown age, or only
/// "17 and Under") fails closed. Evaluated per request, so a runner becomes indexable on their
/// 18th birthday with no extra work.
/// </summary>
public static class SearchIndexing
{
    public const int MinimumIndexableAge = 18;

    /// <param name="dateOfBirth">The runner's verified date of birth, if any.</param>
    /// <param name="latestPossibleBirthDate">The runner's <c>EstimatedDobLatest</c>.</param>
    /// <param name="today">The date to evaluate the age on.</param>
    public static bool IsRunnerIndexable(DateOnly? dateOfBirth, DateOnly? latestPossibleBirthDate, DateOnly today)
    {
        var youngestBirthDate = dateOfBirth ?? latestPossibleBirthDate;
        return youngestBirthDate is { } dob && AgeCalculator.CalculateAge(dob, today) >= MinimumIndexableAge;
    }

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
}
