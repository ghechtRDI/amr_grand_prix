using System.ComponentModel.DataAnnotations;

namespace AmrGrandPrix.API.Models.DTOs;

public class LockUserRequest
{
    [Required]
    public bool Locked { get; set; }
}
