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
    public int? Age { get; set; }
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
            Age = match.Age,
            Gender = match.Gender,
            Confidence = match.Confidence,
            NameMatch = match.NameMatch,
            AgeMatch = match.AgeMatch,
            GenderMatch = match.GenderMatch
        };
    }
}
