using AmrGrandPrix.API.Services.ResultsProcessing;

namespace AmrGrandPrix.API.Models.DTOs.RaceResults;

/// <summary>
/// DTO representing a potential match between a result row and an existing runner
/// </summary>
public class RunnerMatchDto
{
    public Guid RunnerId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";

    /// <summary>
    /// Every name the runner is matched under. Data Review uses it to show when saving will add
    /// the row's name to the runner's alternate names.
    /// </summary>
    public List<string> KnownNames { get; set; } = new();
    public int? Age { get; set; }

    /// <summary>
    /// Youngest/oldest the runner could be on the race date, from their verified DOB (equal) or
    /// estimated birth date range. Null when that side is unknown.
    /// </summary>
    public int? AgeMin { get; set; }
    public int? AgeMax { get; set; }
    public bool HasVerifiedDateOfBirth { get; set; }
    public string? AgeCategory { get; set; }
    public Gender? Gender { get; set; }
    public double Confidence { get; set; }
    public bool NameMatch { get; set; }
    public bool AgeMatch { get; set; }
    public bool GenderMatch { get; set; }

    /// <summary>
    /// Create from RunnerMatch service result
    /// </summary>
    public static RunnerMatchDto FromRunnerMatch(RunnerMatch match)
    {
        return new RunnerMatchDto
        {
            RunnerId = match.RunnerId,
            FirstName = match.FirstName,
            LastName = match.LastName,
            KnownNames = match.KnownNames,
            Age = match.Age,
            AgeMin = match.AgeMin,
            AgeMax = match.AgeMax,
            HasVerifiedDateOfBirth = match.HasVerifiedDateOfBirth,
            AgeCategory = match.AgeCategory,
            Gender = match.Gender,
            Confidence = match.Confidence,
            NameMatch = match.NameMatch,
            AgeMatch = match.AgeMatch,
            GenderMatch = match.GenderMatch
        };
    }
}
