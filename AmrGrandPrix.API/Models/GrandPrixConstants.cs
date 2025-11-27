namespace AmrGrandPrix.API.Models;

/// <summary>
/// Constants and scoring tables for Grand Prix calculations
/// </summary>
public static class GrandPrixConstants
{
    /// <summary>
    /// Total number of Grand Prix races in a season
    /// </summary>
    public const int TotalGrandPrixRaces = 9;

    /// <summary>
    /// Number of best races counted toward final standings
    /// </summary>
    public const int BestRacesCounted = 4;

    /// <summary>
    /// Minimum number of races to qualify for "Run the Gamut"
    /// </summary>
    public const int RunTheGamutMinRaces = 7;

    /// <summary>
    /// Bonus points for setting a new course record (Open Division only)
    /// </summary>
    public const int RecordBonusPoints = 10;

    /// <summary>
    /// Number of top finishers who earn points in Open Division
    /// </summary>
    public const int OpenDivisionTopFinishers = 20;

    /// <summary>
    /// Number of top finishers who earn points in Age Division
    /// </summary>
    public const int AgeDivisionTopFinishers = 5;

    /// <summary>
    /// Open Division points table (FIS Continental Cup scoring system)
    /// Position -> Points
    /// </summary>
    public static readonly Dictionary<int, int> OpenDivisionPoints = new()
    {
        { 1, 100 },
        { 2, 90 },
        { 3, 85 },
        { 4, 80 },
        { 5, 75 },
        { 6, 70 },
        { 7, 65 },
        { 8, 60 },
        { 9, 55 },
        { 10, 50 },
        { 11, 45 },
        { 12, 40 },
        { 13, 35 },
        { 14, 30 },
        { 15, 25 },
        { 16, 20 },
        { 17, 15 },
        { 18, 10 },
        { 19, 5 },
        { 20, 1 }
    };

    /// <summary>
    /// Age Division points table (simplified scoring)
    /// Position -> Points
    /// </summary>
    public static readonly Dictionary<int, int> AgeDivisionPoints = new()
    {
        { 1, 5 },
        { 2, 4 },
        { 3, 3 },
        { 4, 2 },
        { 5, 1 }
    };

    /// <summary>
    /// Age category definitions
    /// </summary>
    public static readonly List<AgeCategory> AgeCategories = new()
    {
        new AgeCategory { Name = "17 and Under", MinAge = 0, MaxAge = 17 },
        new AgeCategory { Name = "19-29", MinAge = 19, MaxAge = 29 },
        new AgeCategory { Name = "30-39", MinAge = 30, MaxAge = 39 },
        new AgeCategory { Name = "40-49", MinAge = 40, MaxAge = 49 },
        new AgeCategory { Name = "50-59", MinAge = 50, MaxAge = 59 },
        new AgeCategory { Name = "60-69", MinAge = 60, MaxAge = 69 },
        new AgeCategory { Name = "70-79", MinAge = 70, MaxAge = 79 },
        new AgeCategory { Name = "80-89", MinAge = 80, MaxAge = 89 }
    };

    /// <summary>
    /// Gets the age category name for a given age
    /// </summary>
    public static string GetAgeCategory(int age)
    {
        var category = AgeCategories.FirstOrDefault(c => age >= c.MinAge && age <= c.MaxAge);
        return category?.Name ?? "Unknown";
    }

    /// <summary>
    /// Gets the Open Division points for a given place
    /// </summary>
    public static int GetOpenDivisionPoints(int place)
    {
        return OpenDivisionPoints.GetValueOrDefault(place, 0);
    }

    /// <summary>
    /// Gets the Age Division points for a given place
    /// </summary>
    public static int GetAgeDivisionPoints(int place)
    {
        return AgeDivisionPoints.GetValueOrDefault(place, 0);
    }
}

/// <summary>
/// Represents an age category with min and max age boundaries
/// </summary>
public class AgeCategory
{
    public string Name { get; set; } = string.Empty;
    public int MinAge { get; set; }
    public int MaxAge { get; set; }
}
