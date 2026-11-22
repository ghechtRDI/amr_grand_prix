using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Services;
using AmrGrandPrix.API.Tests.Infrastructure;

namespace AmrGrandPrix.API.Tests.Controllers;

/// <summary>
/// HTTP-level checks for the public athlete directory (no personal data on the wire) and the
/// admin-only single-runner record used by the "Edit Racer" buttons.
/// </summary>
public class AthleteDirectoryTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private static readonly string[] PersonalFields =
        ["dateOfBirth", "estimatedBirthYear", "email", "alternateNames", "preferredName", "linkedAccountEmail"];

    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public AthleteDirectoryTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.InitializeDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Runner> SeedRunnerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var runner = new Runner
        {
            RunnerId = Guid.NewGuid(),
            FirstName = "Private",
            LastName = "Person",
            Gender = Gender.Female,
            DateOfBirth = new DateOnly(1985, 6, 15),
            EstimatedBirthYear = 1985,
            Email = "private.person@example.com",
            PreferredName = "Priv",
            AlternateNames = ["Maiden Name"]
        };
        db.Runners.Add(runner);
        await db.SaveChangesAsync();
        return runner;
    }

    [Fact]
    public async Task Directory_IsPublic_AndNeverIncludesPersonalFields()
    {
        var runner = await SeedRunnerAsync();

        var response = await _client.GetAsync("/api/runners/directory");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var entry = json.RootElement.EnumerateArray()
            .Single(e => e.GetProperty("runnerId").GetGuid() == runner.RunnerId);
        var keys = entry.EnumerateObject().Select(p => p.Name).ToList();

        keys.Should().NotIntersectWith(PersonalFields);
        body.Should().NotContain("1985-06-15").And.NotContain("private.person@example.com").And.NotContain("Maiden Name");
        entry.GetProperty("fullName").GetString().Should().Be("Private Person");
    }

    [Fact]
    public async Task OpenStandings_DoNotIncludeDateOfBirth()
    {
        var runner = await SeedRunnerAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.GrandPrixStandings.Add(new GrandPrixStanding
            {
                StandingId = Guid.NewGuid(),
                RunnerId = runner.RunnerId,
                Year = 2019,
                Division = Division.OpenFemale,
                Rank = 1,
                TotalPoints = 100
            });
            await db.SaveChangesAsync();
        }

        var body = await _client.GetStringAsync("/api/standings/2019/open/female");

        body.Should().Contain(runner.RunnerId.ToString());
        body.Should().NotContainEquivalentOf("dateOfBirth").And.NotContain("1985-06-15");
    }

    [Fact]
    public async Task RunnerAdminRecord_RequiresAuthentication()
    {
        var runner = await SeedRunnerAsync();

        (await _client.GetAsync($"/api/runners/{runner.RunnerId}/manage")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("ReadOnly", HttpStatusCode.Forbidden)]
    [InlineData("Manager", HttpStatusCode.OK)]
    [InlineData("Admin", HttpStatusCode.OK)]
    public async Task RunnerAdminRecord_IsLimitedToManagersAndAdmins(string role, HttpStatusCode expected)
    {
        var runner = await SeedRunnerAsync();
        var token = await CreateTokenAsync(role);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/runners/{runner.RunnerId}/manage");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(expected);
        if (expected == HttpStatusCode.OK)
        {
            (await response.Content.ReadAsStringAsync()).Should().Contain("Maiden Name");
        }
    }

    [Fact]
    public async Task RunnerAdminRecord_UnknownRunner_ReturnsNotFound()
    {
        var token = await CreateTokenAsync("Admin");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/runners/{Guid.NewGuid()}/manage");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        (await _client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<string> CreateTokenAsync(string role)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        (await userManager.CreateAsync(user, "Test-Password-123!")).Succeeded.Should().BeTrue();
        (await userManager.AddToRoleAsync(user, role)).Succeeded.Should().BeTrue();

        return tokenService.GenerateAccessToken(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role),
        });
    }
}
