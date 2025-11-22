using System.Security.Claims;

namespace AmrGrandPrix.API.Services;

public interface ITokenService
{
    /// <summary>
    /// Generates a JWT access token for the specified user with their claims and roles
    /// </summary>
    string GenerateAccessToken(IEnumerable<Claim> claims);

    /// <summary>
    /// Generates a cryptographically secure random refresh token
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Extracts the claims principal from an expired token (for refresh token validation)
    /// </summary>
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);

    /// <summary>
    /// Validates a token and returns the claims principal if valid
    /// </summary>
    ClaimsPrincipal? ValidateToken(string token);
}
