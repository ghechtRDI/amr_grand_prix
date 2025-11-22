using System.ComponentModel.DataAnnotations;

namespace AmrGrandPrix.API.Models.DTOs;

public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
