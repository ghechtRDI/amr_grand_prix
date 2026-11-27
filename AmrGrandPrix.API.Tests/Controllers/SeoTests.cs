using AmrGrandPrix.API.Common;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Services.Seo;
using AmrGrandPrix.API.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Moq;

namespace AmrGrandPrix.API.Tests.Controllers;

/// <summary>Runners under 18, or of unknown age, stay out of search engines.</summary>
public class SearchIndexingTests
{
    private static readonly DateOnly Today = new(2026, 11, 25);

    [Theory]
    [InlineData("2008-11-25", null, true)]          // 18 today
    [InlineData("2008-11-26", null, false)]         // 18 tomorrow
    [InlineData(null, "2008-11-25", true)]          // youngest possible age is 18
    [InlineData(null, "2008-11-26", false)]         // could still be 17
    [InlineData("2000-01-01", "2010-01-01", true)]  // verified DOB wins over the estimate
    [InlineData(null, null, false)]                 // unknown age (or no upper bound) fails closed
    public void IsRunnerIndexable(string? dob, string? latestPossibleDob, bool expected) =>
        SearchIndexing.IsRunnerIndexable(
                dob is null ? null : DateOnly.Parse(dob),
                latestPossibleDob is null ? null : DateOnly.Parse(latestPossibleDob),
                Today)
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
            DateOfBirth = dob, EstimatedBirthRange = TestData.BornIn(birthYear)
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
    public async Task Build_CategoryOnlyRunners_IndexedOnlyWhenCertainlyAdult()
    {
        // Results that only report an age category: "30-39" is certainly an adult; "17 and Under"
        // never is (no upper bound on the birth date that makes them 18+).
        var race = new Race { RaceId = Guid.NewGuid(), RaceVariant = TestData.Variant("Bird Ridge"), Year = 2025, Date = new DateOnly(2025, 7, 4) };
        _context.Races.Add(race);
        var adult = AddRunner();
        adult.EstimatedBirthRange = BirthDateRange.FromAgeCategory("30-39", race.Date);
        var junior = AddRunner();
        junior.EstimatedBirthRange = BirthDateRange.FromAgeCategory("17 and Under", race.Date);
        AddResult(race, adult, new DateTime(2025, 7, 10, 0, 0, 0, DateTimeKind.Utc));
        AddResult(race, junior, new DateTime(2025, 7, 10, 0, 0, 0, DateTimeKind.Utc));
        await _context.SaveChangesAsync();

        var xml = await new SitemapBuilder(_context).BuildAsync("https://example.org/", Today);

        xml.Should().Contain($"/runners/{adult.RunnerId}");
        xml.Should().NotContain($"/runners/{junior.RunnerId}");
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

    // Seeded once per fixture (xUnit runs InitializeAsync for every test), so the standings
    // leaders don't repeat
    private static Guid _seriesId, _raceId, _adultId, _minorId;
    private static string _seriesName = "";
    private static readonly SemaphoreSlim SeedLock = new(1, 1);

    public SpaFallbackHttpTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeDatabaseAsync();

        await SeedLock.WaitAsync();
        try { await SeedAsync(); }
        finally { SeedLock.Release(); }
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (await db.Runners.AnyAsync(r => r.RunnerId == _adultId))
            return;

        var variant = TestData.Variant($"Series {Guid.NewGuid()}");
        var race = new Race { RaceId = Guid.NewGuid(), RaceVariant = variant, Year = 2031, Date = new DateOnly(2031, 7, 4), Location = "Seward" };
        var adult = new Runner { RunnerId = Guid.NewGuid(), FirstName = "A", LastName = "Adult", Gender = Gender.Male, EstimatedBirthRange = TestData.BornIn(1985) };
        var minor = new Runner { RunnerId = Guid.NewGuid(), FirstName = "M", LastName = "Minor", Gender = Gender.Male, EstimatedBirthRange = TestData.BornIn(DateTime.Today.Year - 12) };
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
        _seriesName = variant.RaceSeries.Name;
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
    [InlineData("/privacy")]
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

    private const string Site = "https://results.alaskamountainrunners.org";

    private async Task<string> GetHtmlAsync(string path) =>
        WebUtility.HtmlDecode(await (await _client.GetAsync(path)).Content.ReadAsStringAsync());

    private static string? Tag(string html, string attr, string key) =>
        Regex.Match(html, $"<meta {attr}=\"{Regex.Escape(key)}\" content=\"([^\"]*)\"") is { Success: true } m
            ? m.Groups[1].Value
            : null;

    private static List<JsonElement> JsonLd(string html) =>
        Regex.Matches(html, "<script type=\"application/ld\\+json\">(.*?)</script>")
            .Select(m => JsonDocument.Parse(m.Groups[1].Value).RootElement)
            .ToList();

    [Fact]
    public async Task Landing_HasSiteMetadata_AndOrganizationJsonLd()
    {
        var html = await GetHtmlAsync("/");

        html.Should().Contain("<title>Alaska Mountain Runners Race Results</title>")
            .And.Contain($"<link rel=\"canonical\" href=\"{Site}/\" />");
        Tag(html, "property", "og:image").Should().Be($"{Site}/og-image.png");
        Tag(html, "name", "twitter:card").Should().Be("summary_large_image");
        Regex.Matches(html, "<title>").Should().HaveCount(1);
        Regex.Matches(html, "<meta name=\"description\"").Should().HaveCount(1);
        JsonLd(html).Select(j => j.GetProperty("@type").GetString()).Should().Equal("Organization", "WebSite");
    }

    [Fact]
    public async Task RacePage_HasRaceTitle_AndSportsEventJsonLd()
    {
        var html = await GetHtmlAsync($"/races/{_raceId.ToString().ToUpperInvariant()}/results");

        html.Should().Contain($"<title>{_seriesName} 2031 Results | Alaska Mountain Runners</title>");
        Tag(html, "name", "description").Should()
            .Be($"Full results for {_seriesName} 2031, run July 4, 2031: 2 results with times, places, and age groups.");
        Tag(html, "property", "og:url").Should().Be($"{Site}/races/{_raceId}/results"); // canonical form of the ID

        var ev = JsonLd(html).Single();
        ev.GetProperty("@type").GetString().Should().Be("SportsEvent");
        ev.GetProperty("startDate").GetString().Should().Be("2031-07-04");
        ev.GetProperty("location").GetProperty("name").GetString().Should().Be("Seward");
    }

    [Fact]
    public async Task StandingsPages_NameTheLeaders_AndCanonicalizeToTheYear()
    {
        var html = await GetHtmlAsync("/standings");

        html.Should().Contain("<title>2031 Grand Prix Standings | Alaska Mountain Runners</title>")
            .And.Contain($"<link rel=\"canonical\" href=\"{Site}/standings/2031\" />");
        Tag(html, "name", "description").Should().EndWith("Leading: A Adult (men).");
    }

    [Fact]
    public async Task AdultRunnerPages_UseTheirName()
    {
        var html = await GetHtmlAsync($"/runners/{_adultId}");
        html.Should().Contain("<title>A Adult – Race Results | Alaska Mountain Runners</title>");
        Tag(html, "property", "og:title").Should().Be("A Adult – Race Results | Alaska Mountain Runners");

        var seriesHtml = await GetHtmlAsync($"/runners/{_adultId}/series/{_seriesId}");
        seriesHtml.Should().Contain($"<title>A Adult – {_seriesName} Results | Alaska Mountain Runners</title>");
    }

    [Fact]
    public async Task MinorRunnerPages_NeverIncludeTheirName()
    {
        foreach (var path in new[] { $"/runners/{_minorId}", $"/runners/{_minorId}/series/{_seriesId}" })
        {
            var html = await GetHtmlAsync(path);
            html.Should().Contain("<title>Runner Profile | Alaska Mountain Runners</title>")
                .And.NotContain("M Minor")
                .And.NotContain("rel=\"canonical\"");
        }
    }

    [Fact]
    public async Task NotFound_HasNotFoundTitle_AndNoCanonical()
    {
        var html = await GetHtmlAsync("/no-such-page");

        html.Should().Contain("<title>Page Not Found | Alaska Mountain Runners</title>").And.NotContain("rel=\"canonical\"");
        Tag(html, "property", "og:url").Should().BeNull();
    }
}

/// <summary>Filled-in values can't break out of their attribute or script tag.</summary>
public class SpaIndexHtmlTests
{
    private static SpaIndexHtml Create()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.WebRootFileProvider).Returns(new NullFileProvider());
        return new SpaIndexHtml(env.Object);
    }

    [Fact]
    public void Render_EscapesValues()
    {
        const string evil = "Bob \"/><script>alert(1)</script>";
        var page = new SpaPage
        {
            Title = evil,
            Description = evil,
            CanonicalPath = "/x",
            JsonLd = [new Dictionary<string, object> { ["name"] = evil }]
        };

        var html = Create().Render(page, "https://example.org");

        html.Should().NotContain("<script>alert").And.NotContain("\"/>&lt;");
        html.Should().Contain("<script type=\"application/ld+json\">");
        Regex.Matches(html, "</script>").Should().HaveCount(1); // only the JSON-LD script's own
    }

    [Fact]
    public void Render_ReplacesTheShellTitle_AndAddsTagsInHead()
    {
        var html = Create().Render(new SpaPage { Title = "Athletes", CanonicalPath = "/athletes" }, "https://example.org");

        html.Should().Contain("<title>Athletes | Alaska Mountain Runners</title>")
            .And.NotContain("<title>Alaska Mountain Runners Race Results</title>");
        html.IndexOf("rel=\"canonical\"", StringComparison.Ordinal).Should()
            .BeLessThan(html.IndexOf("</head>", StringComparison.Ordinal));
    }
}
