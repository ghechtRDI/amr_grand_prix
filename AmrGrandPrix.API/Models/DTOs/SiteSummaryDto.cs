namespace AmrGrandPrix.API.Models.DTOs;

/// <summary>
/// Site-wide totals for the public landing page. Only races with at least one result, and only
/// runners with at least one result, are counted.
/// </summary>
public class SiteSummaryDto
{
    public int RaceCount { get; set; }
    public int YearCount { get; set; }
    public int? FirstYear { get; set; }
    public int? LatestYear { get; set; }
    public int AthleteCount { get; set; }
}
