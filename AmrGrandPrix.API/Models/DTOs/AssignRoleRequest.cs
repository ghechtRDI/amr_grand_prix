using System.ComponentModel.DataAnnotations;

namespace AmrGrandPrix.API.Models.DTOs;

public class AssignRoleRequest
{
    [Required]
    public string RoleName { get; set; } = string.Empty;
}
