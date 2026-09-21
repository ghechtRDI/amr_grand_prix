using System.ComponentModel.DataAnnotations;

namespace AmrGrandPrix.API.Models.DTOs;

public class UpdateUserRequest
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? PreferredName { get; set; }

    public string? Hometown { get; set; }

    [MaxLength(3, ErrorMessage = "Up to 3 alternate names are allowed")]
    public List<string>? AlternateNames { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public Gender? Gender { get; set; }
}
