using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Services;
using AmrGrandPrix.API.Tests.Infrastructure;

namespace AmrGrandPrix.API.Tests.Controllers;

public class UserManagementControllerTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public UserManagementControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetUsers_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserById_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/users/some-user-id");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteUser_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _client.DeleteAsync("/api/users/some-user-id");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AssignRole_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        var request = new { roleName = "Manager" };

        // Act
        var response = await _client.PutAsJsonAsync("/api/users/some-user-id/role", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LockUser_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        var request = new { locked = true };

        // Act
        var response = await _client.PutAsJsonAsync("/api/users/some-user-id/lock", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("ReadOnly")]
    public async Task UserEndpoints_AsNonAdmin_ShouldReturnForbidden(string role)
    {
        var (_, token) = await CreateUserAsync(role);
        var (target, _) = await CreateUserAsync("ReadOnly");

        (await SendAsync(HttpMethod.Get, "/api/users", token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Put, $"/api/users/{target.Id}/role", token, new { roleName = "Admin" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Put, $"/api/users/{target.Id}/lock", token, new { locked = true }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Delete, $"/api/users/{target.Id}", token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetUser_AsAdmin_ShouldReportLockState()
    {
        var (_, token) = await CreateUserAsync("Admin");
        var (target, _) = await CreateUserAsync("ReadOnly");

        var lockResponse = await SendAsync(HttpMethod.Put, $"/api/users/{target.Id}/lock", token, new { locked = true });
        lockResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var user = await (await SendAsync(HttpMethod.Get, $"/api/users/{target.Id}", token))
            .Content.ReadFromJsonAsync<UserResponseShape>();
        user!.IsLocked.Should().BeTrue();

        await SendAsync(HttpMethod.Put, $"/api/users/{target.Id}/lock", token, new { locked = false });
        user = await (await SendAsync(HttpMethod.Get, $"/api/users/{target.Id}", token))
            .Content.ReadFromJsonAsync<UserResponseShape>();
        user!.IsLocked.Should().BeFalse();
    }

    [Fact]
    public async Task AdminActionsOnOwnAccount_ShouldBeRejected()
    {
        var (admin, token) = await CreateUserAsync("Admin");

        (await SendAsync(HttpMethod.Put, $"/api/users/{admin.Id}/role", token, new { roleName = "ReadOnly" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SendAsync(HttpMethod.Put, $"/api/users/{admin.Id}/lock", token, new { locked = true }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SendAsync(HttpMethod.Delete, $"/api/users/{admin.Id}", token))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var stillThere = await userManager.FindByIdAsync(admin.Id);
        stillThere.Should().NotBeNull();
        (await userManager.IsInRoleAsync(stillThere!, "Admin")).Should().BeTrue();
    }

    private sealed record UserResponseShape(string Id, bool IsLocked);

    private async Task<(ApplicationUser User, string Token)> CreateUserAsync(string role)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        (await userManager.CreateAsync(user, "Test-Password-123!")).Succeeded.Should().BeTrue();
        (await userManager.AddToRoleAsync(user, role)).Succeeded.Should().BeTrue();

        var token = tokenService.GenerateAccessToken(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role),
        });
        return (user, token);
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body != null) request.Content = JsonContent.Create(body);
        return _client.SendAsync(request);
    }
}
