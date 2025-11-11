using Microsoft.AspNetCore.Identity;

namespace AmrGrandPrix.API.Services;

public class RoleSeedingService
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<RoleSeedingService> _logger;

    public RoleSeedingService(
        RoleManager<IdentityRole> roleManager,
        ILogger<RoleSeedingService> logger)
    {
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task SeedRolesAsync()
    {
        string[] roleNames = { "ReadOnly", "Manager", "Admin" };

        foreach (var roleName in roleNames)
        {
            var roleExists = await _roleManager.RoleExistsAsync(roleName);
            if (!roleExists)
            {
                var result = await _roleManager.CreateAsync(new IdentityRole(roleName));
                if (result.Succeeded)
                {
                    _logger.LogInformation("Role {RoleName} created successfully", roleName);
                }
                else
                {
                    _logger.LogError("Failed to create role {RoleName}: {Errors}",
                        roleName,
                        string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                _logger.LogInformation("Role {RoleName} already exists", roleName);
            }
        }
    }
}
