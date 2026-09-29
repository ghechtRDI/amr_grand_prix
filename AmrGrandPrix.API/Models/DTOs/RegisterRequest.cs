using System.ComponentModel.DataAnnotations;

namespace AmrGrandPrix.API.Models.DTOs;

public class RegisterRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? PreferredName { get; set; }

    public string? Hometown { get; set; }

    [MaxLength(3, ErrorMessage = "Up to 3 alternate names are allowed")]
    public List<string> AlternateNames { get; set; } = new();

    public DateOnly? DateOfBirth { get; set; }

    /// <summary>
    /// Cloudflare Turnstile response token from the client widget, verified server-side before
    /// the account is created.
    /// </summary>
    [Required]
    public string CaptchaToken { get; set; } = string.Empty;
}
