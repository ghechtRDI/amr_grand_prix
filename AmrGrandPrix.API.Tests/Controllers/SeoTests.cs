using System.Net;
using System.Xml.Linq;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Services.Seo;
using AmrGrandPrix.API.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AmrGrandPrix.API.Tests.Controllers;

/// <summary>Runners under 18, or of unknown age, stay out of search engines.</summary>
public class SearchIndexingTests
{
    private static readonly DateOnly Today = new(2026, 11, 25);

    [Theory]
    [InlineData("2008-11-25", null, true)]  // 18 today
    [InlineData("2008-11-26", null, false)] // 18 tomorrow
    [InlineData(null, 2000, true)]
    [InlineData(null, 2010, false)]
    [InlineData(null, null, false)]         // unknown age fails closed
    public void IsRunnerIndexable(string? dob, int? birthYear, bool expected) =>
        SearchIndexing.IsRunnerIndexable(dob is null ? null : DateOnly.Parse(dob), birthYear, Today)
            .Should().Be(expected);
}

public class SitemapBuilderTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 11, 25);
    private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    private readonly ApplicationDbContext _context;

    public SitemapBuilderTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;
        _context = new ApplicationDbContext(options);
    }

    public void Dispose() => _context.Dispose();

    private Runner AddRunner(DateOnly? dob = null, int? birthYear = null)
    {
        var runner = new Runner
        {
            RunnerId = Guid.NewGuid(), FirstName = "R", LastName = "R", Gender = Gender.Female,
            DateOfBirth = dob, EstimatedBirthYear = birthYear
        };
        _context.Runners.Add(runner);
        return runner;
    }

    private void AddResult(Race race, Runner runner, DateTime createdAt) =>
        _context.RaceResults.Add(new RaceResult
        {
            ResultId = Guid.NewGuid(), RaceId = race.RaceId, RunnerId = runner.RunnerId,
            Status = ResultStatus.Finished, Gender = runner.Gender, UploadBatchId = Guid.NewGuid(),
            CreatedAt = createdAt
        });

    [Fact]
    public async Task Build_ListsPagesWithResults_AndSkipsMinors()
    {
        var variant = TestData.Variant("Mount Marathon");
        var race = new Race { RaceId = Guid.NewGuid(), RaceVariant = variant, Year = 2025, Date = new DateOnly(2025, 7, 4) };
        var emptyRace = new Race { RaceId = Guid.NewGuid(), RaceVariant = TestData.Variant("Empty"), Year = 2026, Date = new DateOnly(2026, 7, 4) };
        _context.Races.AddRange(race, emptyRace);

        var adult = AddRunner(birthYear: 1990);
        var minor = AddRunner(dob: new DateOnly(2012, 1, 1));
        var unknownAge = AddRunner();
        AddRunner(birthYear: 1980); // no results

        AddResult(race, adult, new DateTime(2025, 7, 10, 0, 0, 0, DateTimeKind.Utc));
        AddResult(race, minor, new DateTime(2025, 7, 12, 0, 0, 0, DateTimeKind.Utc));
        AddResult(race, unknownAge, new DateTime(2025, 7, 11, 0, 0, 0, DateTimeKind.Utc));
        _context.GrandPrixStandings.Add(new GrandPrixStanding
        {
            StandingId = Guid.NewGuid(), RunnerId = adult.RunnerId, Year = 2025, Division = Division.OpenFemale, Rank = 1
        });
        await _context.SaveChangesAsync();

        var xml = await new SitemapBuilder(_context).BuildAsync("https://example.org/", Today);

        var urls = XDocument.Parse(xml).Root!.Elements(Ns + "url")
            .ToDictionary(u => u.Element(Ns + "loc")!.Value, u => u.Element(Ns + "lastmod")?.Value);

        urls.Keys.Should().BeEquivalentTo(
            "https://example.org/",
            "https://example.org/race-results",
            "https://example.org/athletes",
            "https://example.org/standings/2025",
            $"https://example.org/race-series/{variant.RaceSeriesId}",
            $"https://example.org/races/{race.RaceId}/results",
            $"https://example.org/runners/{adult.RunnerId}");

        urls[$"https://example.org/races/{race.RaceId}/results"].Should().Be("2025-07-12");
        urls[$"https://example.org/runners/{adult.RunnerId}"].Should().Be("2025-07-10");
        urls["https://example.org/standings/2025"].Should().Be("2025-07-12");
    }

    [Fact]
    public async Task Build_EmptyDatabase_ListsStaticPagesWithoutLastmod()
    {
        var xml = await new SitemapBuilder(_context).BuildAsync("https://example.org", Today);

        var urls = XDocument.Parse(xml).Root!.Elements(Ns + "url").ToList();
        urls.Select(u => u.Element(Ns + "loc")!.Value).Should().Equal(
            "https://example.org/", "https://example.org/race-results", "https://example.org/athletes");
        urls.Should().OnlyContain(u => u.Element(Ns + "lastmod") == null);
    }
}

/// <summary>
/// SPA routes are served with real statuses (404 for unknown paths and missing IDs) and noindex
/// for private pages and minors; robots.txt and sitemap.xml are served from the root.
/// </summary>
public class SpaFallbackHttpTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private Guid _seriesId, _raceId, _adultId, _minorId;

    public SpaFallbackHttpTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeDatabaseAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var variant = TestData.Variant($"Series {Guid.NewGuid()}");
        var race = new Race { RaceId = Guid.NewGuid(), RaceVariant = variant, Year = 2031, Date = new DateOnly(2031, 7, 4) };
        var adult = new Runner { RunnerId = Guid.NewGuid(), FirstName = "A", LastName = "Adult", Gender = Gender.Male, EstimatedBirthYear = 1985 };
        var minor = new Runner { RunnerId = Guid.NewGuid(), FirstName = "M", LastName = "Minor", Gender = Gender.Male, EstimatedBirthYear = DateTime.Today.Year - 12 };
        db.Races.Add(race);
        db.Runners.AddRange(adult, minor);
        foreach (var r in new[] { adult, minor })
            db.RaceResults.Add(new RaceResult
            {
                ResultId = Guid.NewGuid(), RaceId = race.RaceId, RunnerId = r.RunnerId,
                Status = ResultStatus.Finished, Gender = r.Gender, UploadBatchId = Guid.NewGuid()
            });
        db.GrandPrixStandings.Add(new GrandPrixStanding
        {
            StandingId = Guid.NewGuid(), RunnerId = adult.RunnerId, Year = 2031, Division = Division.OpenMale, Rank = 1
        });
        await db.SaveChangesAsync();

        (_seriesId, _raceId, _adultId, _minorId) = (variant.RaceSeriesId, race.RaceId, adult.RunnerId, minor.RunnerId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task AssertPage(HttpResponseMessage response, HttpStatusCode status, bool noIndex)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("<div id=\"root\">");

        response.Headers.TryGetValues("X-Robots-Tag", out var robots).Should().Be(noIndex);
        html.Contains("<meta name=\"robots\" content=\"noindex\" />").Should().Be(noIndex);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/athletes")]
    [InlineData("/Race-Results/")] // case-insensitive, trailing slash, like React Router
    [InlineData("/standings")]
    [InlineData("/standings/2031")]
    public async Task PublicPages_AreIndexable(string path) =>
        await AssertPage(await _client.GetAsync(path), HttpStatusCode.OK, noIndex: false);

    [Theory]
    [InlineData("/login")]
    [InlineData("/profile")]
    [InlineData("/admin/results/upload")]
    [InlineData("/reset-password")]
    public async Task PrivatePages_AreNoIndex(string path) =>
        await AssertPage(await _client.GetAsync(path), HttpStatusCode.OK, noIndex: true);

    [Theory]
    [InlineData("/no-such-page")]
    [InlineData("/admin/nope")]
    [InlineData("/standings/1999")]
    [InlineData("/standings/latest")]
    [InlineData("/races/not-a-guid/results")]
    public async Task UnknownPaths_Are404(string path) =>
        await AssertPage(await _client.GetAsync(path), HttpStatusCode.NotFound, noIndex: true);

    [Fact]
    public async Task ExistingEntities_AreFound_AndMissingOnesAre404()
    {
        await AssertPage(await _client.GetAsync($"/race-series/{_seriesId}"), HttpStatusCode.OK, false);
        await AssertPage(await _client.GetAsync($"/races/{_raceId}/results"), HttpStatusCode.OK, false);
        await AssertPage(await _client.GetAsync($"/runners/{_adultId}"), HttpStatusCode.OK, false);
        await AssertPage(await _client.GetAsync($"/runners/{_adultId}/series/{_seriesId}"), HttpStatusCode.OK, false);

        await AssertPage(await _client.GetAsync($"/race-series/{Guid.NewGuid()}"), HttpStatusCode.NotFound, true);
        await AssertPage(await _client.GetAsync($"/races/{Guid.NewGuid()}/results"), HttpStatusCode.NotFound, true);
        await AssertPage(await _client.GetAsync($"/runners/{Guid.NewGuid()}"), HttpStatusCode.NotFound, true);
        // A series the runner never raced
        await AssertPage(await _client.GetAsync($"/runners/{_adultId}/series/{Guid.NewGuid()}"), HttpStatusCode.NotFound, true);
    }

    [Fact]
    public async Task MinorRunnerPages_AreNoIndex()
    {
        await AssertPage(await _client.GetAsync($"/runners/{_minorId}"), HttpStatusCode.OK, noIndex: true);
        await AssertPage(await _client.GetAsync($"/runners/{_minorId}/series/{_seriesId}"), HttpStatusCode.OK, noIndex: true);
    }

    [Fact]
    public async Task MissingAssetsAndApiRoutes_AreBare404s()
    {
        var asset = await _client.GetAsync("/assets/missing-abc123.js");
        asset.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await asset.Content.ReadAsStringAsync()).Should().NotContain("<div id=\"root\">");

        (await _client.GetAsync("/api/no-such-endpoint")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RobotsTxt_DisallowsPrivatePaths_AndPointsAtSitemap()
    {
        var response = await _client.GetAsync("/robots.txt");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Disallow: /api/").And.Contain("Disallow: /admin/")
            .And.Contain("Sitemap: https://results.alaskamountainrunners.org/sitemap.xml");
    }

    [Fact]
    public async Task SitemapXml_IsServed_WithoutMinors()
    {
        var response = await _client.GetAsync("/sitemap.xml");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/xml");
        var xml = await response.Content.ReadAsStringAsync();
        xml.Should().Contain($"https://results.alaskamountainrunners.org/runners/{_adultId}")
            .And.NotContain(_minorId.ToString());
    }
}
