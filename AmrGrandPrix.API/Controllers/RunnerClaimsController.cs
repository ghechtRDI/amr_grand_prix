using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AmrGrandPrix.API.Common;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs.RaceResults;
using AmrGrandPrix.API.Services.ResultsProcessing;

namespace AmrGrandPrix.API.Controllers;

/// <summary>
/// Lets a runner request that their user account be linked to a historical Runner record
/// ("claim their results"). A claim whose profile-match confidence is >= 90% is approved
/// instantly; 80-89% is queued for admin review; below 80% is rejected up front and the caller
/// is pointed at the public report form instead.
/// </summary>
[ApiController]
[Route("api/runner-claims")]
public class RunnerClaimsController : ControllerBase
{
    private const int AgeMismatchToleranceYears = 1;
    private const double AutoApproveConfidenceThreshold = 0.90;
    private const double ReviewQueueConfidenceThreshold = 0.80;

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRunnerMatchingService _matchingService;
    private readonly ILogger<RunnerClaimsController> _logger;

    public RunnerClaimsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IRunnerMatchingService matchingService,
        ILogger<RunnerClaimsController> logger)
    {
        _context = context;
        _userManager = userManager;
        _matchingService = matchingService;
        _logger = logger;
    }

    /// <summary>
    /// Suggested unclaimed Runner records for the current user, for the "Is this you?" self-serve
    /// claim surface. Each match reports the confidence band the frontend should offer:
    /// instant-claim (>= 90%), request-review (80-89%), or neither (below 80%, point at the
    /// report form instead).
    /// </summary>
    [HttpGet("suggested-matches")]
    [Authorize]
    [ProducesResponseType(typeof(List<RunnerMatchDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RunnerMatchDto>>> GetSuggestedMatches()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized();

        var matches = await _matchingService.FindMatchesForProfileAsync(user);

        return Ok(matches
            .Where(m => m.Confidence >= ReviewQueueConfidenceThreshold)
            .Select(RunnerMatchDto.FromRunnerMatch)
            .ToList());
    }

    /// <summary>
    /// Submit a request to claim a Runner record. The caller must have already set a verified
    /// date of birth on their own profile (see PUT /api/auth/profile). Confidence is computed
    /// server-side — a client-supplied score is never trusted.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(RunnerClaimDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RunnerClaimDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RunnerClaimDto>> SubmitClaim([FromBody] SubmitClaimRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized();

        if (!user.DateOfBirth.HasValue)
            return BadRequest(new { message = "Set your date of birth in your profile before claiming a runner" });

        var runner = await _context.Runners.FindAsync(request.RunnerId);
        if (runner == null)
            return NotFound($"Runner with ID {request.RunnerId} not found");

        var alreadyPendingOrApproved = await _context.RunnerClaims.AnyAsync(c =>
            c.ApplicationUserId == user.Id &&
            c.RunnerId == request.RunnerId &&
            (c.Status == ClaimStatus.Pending || c.Status == ClaimStatus.Approved));

        if (alreadyPendingOrApproved)
            return Conflict(new { message = "You already have a pending or approved claim on this runner" });

        var candidateMatches = await _matchingService.FindMatchesForProfileAsync(user);
        var confidence = candidateMatches.FirstOrDefault(m => m.RunnerId == request.RunnerId)?.Confidence ?? 0.0;

        if (confidence < ReviewQueueConfidenceThreshold)
        {
            return BadRequest(new
            {
                message = "This runner doesn't match your profile closely enough to claim automatically. " +
                           "Please use the \"Report an issue\" form so our team can review it.",
                confidence
            });
        }

        var claim = new RunnerClaim
        {
            ClaimId = Guid.NewGuid(),
            ApplicationUserId = user.Id,
            RunnerId = request.RunnerId,
            Status = ClaimStatus.Pending,
            RequestedAt = DateTime.UtcNow,
            MatchConfidence = confidence
        };

        _context.RunnerClaims.Add(claim);

        if (confidence >= AutoApproveConfidenceThreshold)
        {
            ApplyApproval(claim, user, runner, admin: null);
            _logger.LogInformation(
                "User {UserId} auto-claimed runner {RunnerId} (confidence: {Confidence:P0})",
                user.Id, request.RunnerId, confidence);
        }
        else
        {
            _logger.LogInformation(
                "User {UserId} submitted a claim {ClaimId} on runner {RunnerId} for review (confidence: {Confidence:P0})",
                user.Id, claim.ClaimId, request.RunnerId, confidence);
        }

        await _context.SaveChangesAsync();

        var dto = await BuildClaimDtoAsync(claim, user, runner);
        return claim.Status == ClaimStatus.Approved
            ? Ok(dto)
            : CreatedAtAction(nameof(GetClaims), null, dto);
    }

    /// <summary>
    /// List runner claims, optionally filtered by status. Admin only.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(typeof(List<RunnerClaimDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RunnerClaimDto>>> GetClaims([FromQuery] ClaimStatus? status = null)
    {
        var query = _context.RunnerClaims
            .Include(c => c.ApplicationUser)
            .Include(c => c.Runner)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        var claims = await query.OrderByDescending(c => c.RequestedAt).ToListAsync();

        var dtos = new List<RunnerClaimDto>();
        foreach (var claim in claims)
            dtos.Add(await BuildClaimDtoAsync(claim, claim.ApplicationUser, claim.Runner));

        return Ok(dtos);
    }

    /// <summary>
    /// Approve a pending claim: links the user's account to the Runner and makes the user's
    /// verified date of birth the runner's authoritative date of birth. Admin only.
    /// </summary>
    [HttpPost("{id}/approve")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveClaim(Guid id)
    {
        var claim = await _context.RunnerClaims
            .Include(c => c.ApplicationUser)
            .Include(c => c.Runner)
            .FirstOrDefaultAsync(c => c.ClaimId == id);

        if (claim == null)
            return NotFound($"Claim {id} not found");

        if (claim.Status != ClaimStatus.Pending)
            return BadRequest(new { message = $"Only pending claims can be approved (claim is {claim.Status})" });

        var admin = await _userManager.GetUserAsync(User);

        ApplyApproval(claim, claim.ApplicationUser, claim.Runner, admin);

        await _context.SaveChangesAsync();

        _logger.LogInformation("Claim {ClaimId} approved by {AdminId}, linking user {UserId} to runner {RunnerId}",
            claim.ClaimId, admin?.Id, claim.ApplicationUserId, claim.RunnerId);

        return Ok(new { message = "Claim approved" });
    }

    /// <summary>
    /// Reject a pending claim. Admin only.
    /// </summary>
    [HttpPost("{id}/reject")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectClaim(Guid id, [FromBody] RejectClaimRequest request)
    {
        var claim = await _context.RunnerClaims.FindAsync(id);
        if (claim == null)
            return NotFound($"Claim {id} not found");

        if (claim.Status != ClaimStatus.Pending)
            return BadRequest(new { message = $"Only pending claims can be rejected (claim is {claim.Status})" });

        var admin = await _userManager.GetUserAsync(User);

        claim.Status = ClaimStatus.Rejected;
        claim.ReviewedAt = DateTime.UtcNow;
        claim.ReviewedByUserId = admin?.Id;
        claim.Notes = request.Notes;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Claim {ClaimId} rejected by {AdminId}", claim.ClaimId, admin?.Id);

        return Ok(new { message = "Claim rejected" });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Links the user's account to the runner and syncs the user's verified profile data (date of
    /// birth, preferred name, alternate names) onto the runner as its new authoritative values —
    /// shared by both the auto-approval path in <see cref="SubmitClaim"/> and the admin-driven
    /// <see cref="ApproveClaim"/>. <paramref name="admin"/> is null for an auto-approval.
    /// </summary>
    private static void ApplyApproval(RunnerClaim claim, ApplicationUser user, Runner runner, ApplicationUser? admin)
    {
        claim.Status = ClaimStatus.Approved;
        claim.ReviewedAt = DateTime.UtcNow;
        claim.ReviewedByUserId = admin?.Id;

        user.RunnerId = runner.RunnerId;
        runner.DateOfBirth = user.DateOfBirth;
        runner.PreferredName = user.PreferredName;
        runner.AlternateNames = user.AlternateNames;
        runner.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Builds a claim DTO with a per-result age-plausibility check: the claimant's date of birth
    /// compared against each of the runner's recorded race results, as of each race's date.
    /// Informational only — never blocks a claim from being submitted or reviewed.
    /// </summary>
    private async Task<RunnerClaimDto> BuildClaimDtoAsync(RunnerClaim claim, ApplicationUser user, Runner runner)
    {
        var results = await _context.RaceResults
            .Include(r => r.Race)
            .Where(r => r.RunnerId == runner.RunnerId && r.Age.HasValue)
            .OrderByDescending(r => r.Race.Date)
            .ToListAsync();

        var ageChecks = results.Select(r =>
        {
            var expectedAge = user.DateOfBirth.HasValue
                ? AgeCalculator.CalculateAge(user.DateOfBirth.Value, r.Race.Date)
                : (int?)null;

            return new ClaimAgeCheckDto
            {
                ResultId = r.ResultId,
                RaceName = r.Race.Name,
                RaceDate = r.Race.Date,
                RecordedAge = r.Age,
                ExpectedAge = expectedAge,
                IsMismatch = expectedAge.HasValue && r.Age.HasValue &&
                             Math.Abs(expectedAge.Value - r.Age.Value) > AgeMismatchToleranceYears
            };
        }).ToList();

        return new RunnerClaimDto
        {
            ClaimId = claim.ClaimId,
            ApplicationUserId = claim.ApplicationUserId,
            UserEmail = user.Email ?? string.Empty,
            RunnerId = claim.RunnerId,
            RunnerName = runner.FullName,
            Status = claim.Status,
            RequestedAt = claim.RequestedAt,
            ReviewedAt = claim.ReviewedAt,
            Notes = claim.Notes,
            MatchConfidence = claim.MatchConfidence,
            AgeChecks = ageChecks
        };
    }
}

public class SubmitClaimRequest
{
    public Guid RunnerId { get; set; }
}

public class RejectClaimRequest
{
    public string? Notes { get; set; }
}

public class RunnerClaimDto
{
    public Guid ClaimId { get; set; }
    public string ApplicationUserId { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public Guid RunnerId { get; set; }
    public string RunnerName { get; set; } = string.Empty;
    public ClaimStatus Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? Notes { get; set; }
    public double? MatchConfidence { get; set; }
    public List<ClaimAgeCheckDto> AgeChecks { get; set; } = new();
}

/// <summary>
/// Plausibility check for one of the claimed runner's race results: does the claimant's date of
/// birth imply an age consistent with what was recorded for that race?
/// </summary>
public class ClaimAgeCheckDto
{
    public Guid ResultId { get; set; }
    public string RaceName { get; set; } = string.Empty;
    public DateOnly RaceDate { get; set; }
    public int? RecordedAge { get; set; }
    public int? ExpectedAge { get; set; }
    public bool IsMismatch { get; set; }
}
