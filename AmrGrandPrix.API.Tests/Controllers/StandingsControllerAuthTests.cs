using System.Net;
using FluentAssertions;
using AmrGrandPrix.API.Tests.Infrastructure;

namespace AmrGrandPrix.API.Tests.Controllers;

public class StandingsControllerAuthTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public StandingsControllerAuthTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.InitializeDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("/api/standings/2026/recalculate")]
    [InlineData("/api/standings/2026/finalize")]
    [InlineData("/api/standings/2026/unfinalize")]
    public async Task AdminActions_WithoutAuthentication_ReturnUnauthorized(string url)
    {
        var response = await _client.PostAsync(url, null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PublicReads_WithoutAuthentication_AreAllowed()
    {
        (await _client.GetAsync("/api/standings/years")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetAsync("/api/standings/2026/season")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
