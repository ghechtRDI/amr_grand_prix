namespace AmrGrandPrix.API.Models.DTOs;

/// <summary>
/// Admin view of a runner record: identity fields (including the alternate names used for
/// matching) plus a summary of the results attached to it.
/// </summary>
public class RunnerAdminDto
{
    public Guid RunnerId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PreferredName { get; set; }
    public List<string> AlternateNames { get; set; } = new();
    public Gender Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public int? EstimatedBirthYear { get; set; }
    public string? Email { get; set; }
    public int ResultCount { get; set; }
    public int? FirstYear { get; set; }
    public int? LastYear { get; set; }

    /// <summary>Email of the user account linked to this runner via an approved claim, if any.</summary>
    public string? LinkedAccountEmail { get; set; }
}

/// <summary>
/// Two runner records that look like the same person.
/// </summary>
public class PotentialDuplicateDto
{
    public RunnerAdminDto RunnerA { get; set; } = null!;
    public RunnerAdminDto RunnerB { get; set; } = null!;

    /// <summary>Overall match confidence (0.0-1.0): name similarity corroborated by gender and birth year.</summary>
    public double Confidence { get; set; }

    /// <summary>Best similarity across each runner's legal, preferred, and alternate names.</summary>
    public double NameSimilarity { get; set; }

    public bool GenderMatch { get; set; }

    /// <summary>Null when either runner has no known birth year, so it can't be compared.</summary>
    public bool? BirthYearMatch { get; set; }
}

/// <summary>
/// What merging <see cref="Source"/> into <see cref="Target"/> would do. Nothing is changed by
/// producing a preview.
/// </summary>
public class RunnerMergePreviewDto
{
    /// <summary>The record that is kept.</summary>
    public RunnerAdminDto Target { get; set; } = null!;

    /// <summary>The record that is folded into the target and then deleted.</summary>
    public RunnerAdminDto Source { get; set; } = null!;

    /// <summary>The target's identity fields as they will be after the merge.</summary>
    public MergedRunnerDto Merged { get; set; } = null!;

    public int ResultsToMove { get; set; }

    /// <summary>Grand Prix years whose standings will be recalculated.</summary>
    public List<int> GrandPrixYearsToRecalculate { get; set; } = new();

    /// <summary>Finalized Grand Prix years the merge would change — these block the merge.</summary>
    public List<int> FinalizedYears { get; set; } = new();

    /// <summary>True when the source's linked user account will be re-linked to the target.</summary>
    public bool MovesAccountLink { get; set; }

    /// <summary>Things worth double-checking that don't prevent the merge.</summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>Reasons the merge can't go ahead; empty when <see cref="CanMerge"/>.</summary>
    public List<string> BlockingIssues { get; set; } = new();

    public bool CanMerge => BlockingIssues.Count == 0;
}

public class MergedRunnerDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PreferredName { get; set; }
    public List<string> AlternateNames { get; set; } = new();
    public Gender Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public int? EstimatedBirthYear { get; set; }
    public string? Email { get; set; }
}

public class MergeRunnersRequest
{
    public Guid SourceRunnerId { get; set; }
}

public class RunnerMergeResultDto
{
    public RunnerAdminDto Runner { get; set; } = null!;
    public int ResultsMoved { get; set; }
    public List<int> YearsRecalculated { get; set; } = new();
}
