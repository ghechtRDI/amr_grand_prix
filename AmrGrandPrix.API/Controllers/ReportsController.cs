using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs;
using AmrGrandPrix.API.Services;
using AmrGrandPrix.API.Services.Captcha;

namespace AmrGrandPrix.API.Controllers;

/// <summary>
/// Public "report an inaccurate result" / "help me claim a result" form. Persists each
/// submission for an admin audit trail and emails the club.
/// </summary>
[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private const string ClubEmail = "akmtnrunners@gmail.com";

    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ICaptchaService _captchaService;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        ApplicationDbContext context,
        IEmailService emailService,
        ICaptchaService captchaService,
        ILogger<ReportsController> logger)
    {
        _context = context;
        _emailService = emailService;
        _captchaService = captchaService;
        _logger = logger;
    }

    /// <summary>
    /// Submit a report. Anonymous, bot-gated (Turnstile + rate limit).
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("PublicFormSubmission")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitReport([FromBody] SubmitReportRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var captchaValid = await _captchaService.VerifyAsync(request.CaptchaToken, HttpContext.Connection.RemoteIpAddress?.ToString());
        if (!captchaValid)
            return BadRequest(new { message = "Captcha verification failed. Please try again." });

        Race? race = null;
        if (request.RaceId.HasValue)
            race = await _context.Races
                .Include(r => r.RaceVariant).ThenInclude(v => v.RaceSeries).ThenInclude(s => s.Variants)
                .FirstOrDefaultAsync(r => r.RaceId == request.RaceId.Value);

        var report = new ResultReport
        {
            ReportId = Guid.NewGuid(),
            RunnerNameReported = request.RunnerName,
            DateOfBirthReported = request.DateOfBirth,
            RaceId = race?.RaceId,
            RaceName = race != null ? RaceProjections.DisplayName(race) : request.RaceName,
            RaceDate = request.RaceDate,
            Description = request.Description,
            ReporterEmail = request.ReporterEmail,
            Status = ReportStatus.New,
            SubmittedAt = DateTime.UtcNow
        };

        _context.ResultReports.Add(report);
        await _context.SaveChangesAsync();

        var body = $"""
            A new result report was submitted on the Grand Prix site.

            Runner name: {report.RunnerNameReported}
            Date of birth: {report.DateOfBirthReported?.ToString() ?? "(not provided)"}
            Race: {report.RaceName ?? "(not specified)"}
            Race date: {report.RaceDate?.ToString() ?? "(not specified)"}
            Reporter email: {report.ReporterEmail}

            Description:
            {report.Description}
            """;

        await _emailService.SendEmailAsync(ClubEmail, "Race result report submitted", body);

        _logger.LogInformation("Result report {ReportId} submitted for runner '{RunnerName}'",
            report.ReportId, report.RunnerNameReported);

        return Ok(new { message = "Thanks — your report has been sent to the race committee." });
    }

    /// <summary>
    /// List submitted reports. Admin/Manager only.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(List<ResultReportDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ResultReportDto>>> GetReports([FromQuery] ReportStatus? status = null)
    {
        var query = _context.ResultReports.AsQueryable();
        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        var reports = await query
            .OrderByDescending(r => r.SubmittedAt)
            .Select(r => new ResultReportDto
            {
                ReportId = r.ReportId,
                RunnerNameReported = r.RunnerNameReported,
                DateOfBirthReported = r.DateOfBirthReported,
                RaceId = r.RaceId,
                RaceName = r.RaceName,
                RaceDate = r.RaceDate,
                Description = r.Description,
                ReporterEmail = r.ReporterEmail,
                Status = r.Status,
                SubmittedAt = r.SubmittedAt
            })
            .ToListAsync();

        return Ok(reports);
    }

    /// <summary>
    /// Mark a report as reviewed. Admin/Manager only.
    /// </summary>
    [HttpPost("{id}/mark-reviewed")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkReviewed(Guid id)
    {
        var report = await _context.ResultReports.FindAsync(id);
        if (report == null)
            return NotFound();

        report.Status = ReportStatus.Reviewed;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Report marked as reviewed" });
    }
}

public class SubmitReportRequest
{
    [Required]
    [MaxLength(200)]
    public string RunnerName { get; set; } = string.Empty;

    public DateOnly? DateOfBirth { get; set; }
    public Guid? RaceId { get; set; }

    [MaxLength(200)]
    public string? RaceName { get; set; }

    public DateOnly? RaceDate { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string ReporterEmail { get; set; } = string.Empty;

    [Required]
    public string CaptchaToken { get; set; } = string.Empty;
}

public class ResultReportDto
{
    public Guid ReportId { get; set; }
    public string RunnerNameReported { get; set; } = string.Empty;
    public DateOnly? DateOfBirthReported { get; set; }
    public Guid? RaceId { get; set; }
    public string? RaceName { get; set; }
    public DateOnly? RaceDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ReporterEmail { get; set; } = string.Empty;
    public ReportStatus Status { get; set; }
    public DateTime SubmittedAt { get; set; }
}
