using AmrGrandPrix.API.Models.DTOs;

namespace AmrGrandPrix.API.Services.Runners;

/// <summary>
/// Finds runner records that look like the same person and folds one into another.
/// </summary>
public interface IRunnerMergeService
{
    /// <summary>
    /// Pairs of runners whose best name similarity (legal, preferred, and alternate names) is at
    /// least 75% and whose overall confidence — corroborated by gender and birth year — is at
    /// least <paramref name="minConfidence"/>. Pairs with results in the same race are left out,
    /// since one person can't have two results in one race. Highest confidence first.
    /// </summary>
    Task<List<PotentialDuplicateDto>> FindPotentialDuplicatesAsync(double minConfidence = 0.75, int limit = 200);

    /// <summary>Admin summaries for the given runners, keyed by runner ID.</summary>
    Task<Dictionary<Guid, RunnerAdminDto>> GetAdminSummariesAsync(IReadOnlyCollection<Guid> runnerIds);

    /// <summary>
    /// What merging <paramref name="sourceId"/> into <paramref name="targetId"/> would do, without
    /// changing anything. Null if either runner doesn't exist.
    /// </summary>
    Task<RunnerMergePreviewDto?> PreviewMergeAsync(Guid targetId, Guid sourceId);

    /// <summary>
    /// Moves every result, Grand Prix points record, claim, and account link from the source
    /// runner to the target, adds both runners' names to the target's alternate names, fills in
    /// any identity fields the target is missing from the source, deletes the source, and
    /// recalculates the affected Grand Prix standings. Callers must check
    /// <see cref="PreviewMergeAsync"/>'s <see cref="RunnerMergePreviewDto.CanMerge"/> first;
    /// throws <see cref="InvalidOperationException"/> if the merge is blocked.
    /// </summary>
    Task<RunnerMergeResultDto> MergeAsync(Guid targetId, Guid sourceId);
}
