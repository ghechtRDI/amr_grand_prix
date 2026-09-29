namespace AmrGrandPrix.API.Services.GrandPrix;

/// <summary>
/// Thrown when an operation would change points or standings for a finalized Grand Prix season.
/// </summary>
public class GrandPrixSeasonFinalizedException : InvalidOperationException
{
    public int Year { get; }

    public GrandPrixSeasonFinalizedException(int year)
        : base(MessageFor(year))
    {
        Year = year;
    }

    public static string MessageFor(int year) =>
        $"The {year} Grand Prix is finalized, so its points and standings can't change. " +
        "Un-finalize it in Results Management to make corrections, then finalize it again.";
}
