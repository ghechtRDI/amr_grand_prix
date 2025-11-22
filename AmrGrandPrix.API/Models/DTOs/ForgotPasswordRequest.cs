using System.ComponentModel.DataAnnotations;

namespace AmrGrandPrix.API.Models.DTOs;

public class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
