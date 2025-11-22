namespace AmrGrandPrix.API.Models.DTOs;

public class UpdateUserRequest
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public DateOnly? DateOfBirth { get; set; }
}
