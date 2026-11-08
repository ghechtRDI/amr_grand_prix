using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs;
using AmrGrandPrix.API.Tests.Infrastructure;

namespace AmrGrandPrix.API.Tests.Controllers;

public class AuthControllerTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public AuthControllerTests(CustomWebApplicationFactory factory)
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
    public async Task Register_WithValidData_ShouldReturnOk()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = $"test{Guid.NewGuid()}@example.com",
            Password = "Test@1234",
            ConfirmPassword = "Test@1234",
            FirstName = "Test",
            LastName = "User",
            CaptchaToken = "test-token"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("message");
    }

    [Fact]
    public async Task Register_WithMismatchedPasswords_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "Test@1234",
            ConfirmPassword = "Different@1234",
            FirstName = "Test",
            LastName = "User",
            CaptchaToken = "test-token"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ShouldReturnBadRequest()
    {
        // Arrange
        var email = $"duplicate{Guid.NewGuid()}@example.com";
        var request = new RegisterRequest
        {
            Email = email,
            Password = "Test@1234",
            ConfirmPassword = "Test@1234",
            FirstName = "Test",
            LastName = "User",
            CaptchaToken = "test-token"
        };

        // Act - Register first time
        await _client.PostAsJsonAsync("/api/auth/register", request);

        // Act - Try to register with same email
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WithoutEmailConfirmation_ShouldReturnUnauthorized()
    {
        // Arrange - Register a user
        var registerRequest = new RegisterRequest
        {
            Email = $"unconfirmed{Guid.NewGuid()}@example.com",
            Password = "Test@1234",
            ConfirmPassword = "Test@1234",
            CaptchaToken = "test-token"
        };
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Act - Try to login without confirming email
        var loginRequest = new LoginRequest
        {
            Email = registerRequest.Email,
            Password = registerRequest.Password
        };
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShouldReturnUnauthorized()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Email = "nonexistent@example.com",
            Password = "WrongPassword@123"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshToken_WithInvalidToken_ShouldReturnUnauthorized()
    {
        // Arrange
        var request = new RefreshTokenRequest
        {
            RefreshToken = "invalid-refresh-token"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh-token", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshToken_WithValidToken_ShouldReturnNewTokens()
    {
        // Arrange - create a confirmed user directly (bypassing the rate-limited register
        // endpoint, which other tests in this class also share a quota with), then log in
        // to obtain a real refresh token
        var email = $"refresh{Guid.NewGuid()}@example.com";
        const string password = "Test@1234";

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = "Test",
                LastName = "User",
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await userManager.CreateAsync(user, password);
        }

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });
        var loginData = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh-token", new RefreshTokenRequest
        {
            RefreshToken = loginData!.RefreshToken
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        content.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        content.GetProperty("refreshToken").GetString().Should().NotBeNullOrEmpty()
            .And.NotBe(loginData.RefreshToken);
    }

    private async Task<string> CreateConfirmedUserAsync(string prefix, string password)
    {
        var email = $"{prefix}{Guid.NewGuid()}@example.com";
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await userManager.CreateAsync(new ApplicationUser
        {
            UserName = email, Email = email, FirstName = "Test", LastName = "User",
            EmailConfirmed = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        }, password);
        return email;
    }

    [Fact]
    public async Task ForgotPassword_ThenResetWithEmailedLink_LetsUserLogInWithNewPassword()
    {
        var email = await CreateConfirmedUserAsync("reset+tag", "Old@12345");

        var forgot = await _client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest { Email = email });
        forgot.StatusCode.Should().Be(HttpStatusCode.OK);

        // Pull the link out of the email the way the SPA's /reset-password page reads it.
        var body = _factory.FakeEmailService.SentEmails.Last(e => e.To == email).Body;
        var link = new Uri(System.Text.RegularExpressions.Regex.Match(body, @"https?://\S+").Value);
        link.AbsolutePath.Should().Be("/reset-password");
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(link.Query);
        query["email"].ToString().Should().Be(email, "a '+' in the address must survive the query string");

        var reset = await _client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest
        {
            Email = query["email"].ToString(),
            Token = query["token"].ToString(),
            NewPassword = "New@12345",
            ConfirmPassword = "New@12345"
        });
        reset.StatusCode.Should().Be(HttpStatusCode.OK);

        var oldLogin = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = "Old@12345" });
        oldLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var newLogin = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = "New@12345" });
        newLogin.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_ReturnsBadRequestWithReason()
    {
        var email = await CreateConfirmedUserAsync("badtoken", "Old@12345");

        var reset = await _client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest
        {
            Email = email, Token = "not-a-real-token", NewPassword = "New@12345", ConfirmPassword = "New@12345"
        });

        reset.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await reset.Content.ReadAsStringAsync()).Should().Contain("Invalid token");
    }
}
