namespace AmrGrandPrix.API.Models.DTOs;

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime TokenExpiration { get; set; }
    public UserResponse User { get; set; } = null!;
}
