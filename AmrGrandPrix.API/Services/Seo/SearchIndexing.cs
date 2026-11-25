using AmrGrandPrix.API.Common;
using AmrGrandPrix.API.Models;

namespace AmrGrandPrix.API.Services.Seo;

/// <summary>
/// Which runner pages search engines may index. Runners under 18 are kept out (noindex, left out
/// of the sitemap); a runner with no date of birth and no estimated birth year counts as a minor,
/// so an unknown age fails closed. Evaluated per request, so a runner becomes indexable on their
/// 18th birthday with no extra work.
/// </summary>
public static class SearchIndexing
{
    public const int MinimumIndexableAge = 18;

    public static bool IsRunnerIndexable(DateOnly? dateOfBirth, int? estimatedBirthYear, DateOnly today)
    {
        var age = AgeCalculator.GetRunnerAge(
            new Runner { DateOfBirth = dateOfBirth, EstimatedBirthYear = estimatedBirthYear }, today);
        return age >= MinimumIndexableAge;
    }

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
}
