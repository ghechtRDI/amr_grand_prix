using System.Linq.Expressions;

namespace AmrGrandPrix.API.Models.DTOs;

/// <summary>
/// DTO for Race data transfer
/// </summary>
public class RaceDto
{
    public Guid RaceId { get; set; }

    /// <summary>The series name, e.g. "Kal's Knoya Ridge Run".</summary>
    public string Name { get; set; } = string.Empty;
    public bool IsGrandPrixRace { get; set; }
    public DateOnly Date { get; set; }
    public int Year { get; set; }
    public Guid RaceVariantId { get; set; }

    /// <summary>
    /// The variant name, e.g. "Full Monty". Null when the series has only one variant, so
    /// single-course races don't show a redundant "Standard" label.
    /// </summary>
    public string? CourseVariant { get; set; }
    public string? Location { get; set; }
    public Guid RaceSeriesId { get; set; }
    public string RaceSeriesName { get; set; } = string.Empty;
    public TimeSpan? RecordTimeMale { get; set; }
    public TimeSpan? RecordTimeFemale { get; set; }
    public string? RecordHolderMale { get; set; }
    public string? RecordHolderFemale { get; set; }
    public int ResultsCount { get; set; }
}

public static class RaceProjections
{
    /// <summary>
    /// A single label for a race, e.g. "Mount Marathon Race – Junior", or just the series name
    /// when the series has one variant. Safe to call in a top-level EF projection (client-evaluated).
    /// </summary>
    public static string DisplayName(string seriesName, string variantName, int seriesVariantCount) =>
        seriesVariantCount > 1 ? $"{seriesName} – {variantName}" : seriesName;

    /// <summary>
    /// <see cref="DisplayName(string, string, int)"/> for a race whose
    /// <c>RaceVariant.RaceSeries.Variants</c> navigations are loaded.
    /// </summary>
    public static string DisplayName(Race race) =>
        DisplayName(race.RaceVariant.RaceSeries.Name, race.RaceVariant.Name, race.RaceVariant.RaceSeries.Variants.Count);

    /// <summary>EF-translatable projection from <see cref="Race"/> to <see cref="RaceDto"/>.</summary>
    public static readonly Expression<Func<Race, RaceDto>> ToDto = r => new RaceDto
    {
        RaceId = r.RaceId,
        Name = r.RaceVariant.RaceSeries.Name,
        IsGrandPrixRace = r.IsGrandPrixRace,
        Date = r.Date,
        Year = r.Year,
        RaceVariantId = r.RaceVariantId,
        CourseVariant = r.RaceVariant.RaceSeries.Variants.Count > 1 ? r.RaceVariant.Name : null,
        Location = r.Location,
        RaceSeriesId = r.RaceVariant.RaceSeriesId,
        RaceSeriesName = r.RaceVariant.RaceSeries.Name,
        RecordTimeMale = r.RaceVariant.RecordTimeMale,
        RecordTimeFemale = r.RaceVariant.RecordTimeFemale,
        RecordHolderMale = r.RaceVariant.RecordHolderMale,
        RecordHolderFemale = r.RaceVariant.RecordHolderFemale,
        ResultsCount = r.Results.Count
    };
}

/// <summary>
/// Request for creating one year's running of a race variant
/// </summary>
public class CreateRaceRequest
{
    public Guid RaceVariantId { get; set; }
    public DateOnly Date { get; set; }

    /// <summary>Defaults to the variant's <see cref="RaceVariant.IsGrandPrixByDefault"/> when null.</summary>
    public bool? IsGrandPrixRace { get; set; }
    public string? Location { get; set; }
}

/// <summary>
/// Request for updating an existing race
/// </summary>
public class UpdateRaceRequest
{
    public Guid RaceVariantId { get; set; }
    public DateOnly Date { get; set; }
    public bool IsGrandPrixRace { get; set; }
    public string? Location { get; set; }
}
