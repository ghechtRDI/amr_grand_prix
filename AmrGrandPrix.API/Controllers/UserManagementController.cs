using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs;

namespace AmrGrandPrix.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = "Admin")]
public class UserManagementController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<UserManagementController> _logger;

    public UserManagementController(
        UserManager<ApplicationUser> userManager,
        ILogger<UserManagementController> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Get all users with pagination
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = _userManager.Users.AsQueryable();

        // Search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                u.Email!.Contains(search) ||
                (u.FirstName != null && u.FirstName.Contains(search)) ||
                (u.LastName != null && u.LastName.Contains(search)));
        }

        var totalUsers = await query.CountAsync();
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var userResponses = new List<UserResponse>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userResponses.Add(new UserResponse
            {
                Id = user.Id,
                Email = user.Email!,
                FirstName = user.FirstName,
                LastName = user.LastName,
                DateOfBirth = user.DateOfBirth,
                EmailConfirmed = user.EmailConfirmed,
                Roles = roles.ToList(),
                CreatedAt = user.CreatedAt
            });
        }

        return Ok(new
        {
            users = userResponses,
            page,
            pageSize,
            totalUsers,
            totalPages = (int)Math.Ceiling(totalUsers / (double)pageSize)
        });
    }

    /// <summary>
    /// Get user by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound(new { message = "User not found" });

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new UserResponse
        {
            Id = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            DateOfBirth = user.DateOfBirth,
            EmailConfirmed = user.EmailConfirmed,
            Roles = roles.ToList(),
            CreatedAt = user.CreatedAt
        });
    }

    /// <summary>
    /// Assign or change user role
    /// </summary>
    [HttpPut("{id}/role")]
    public async Task<IActionResult> AssignRole(string id, [FromBody] AssignRoleRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound(new { message = "User not found" });

        // Validate role exists
        var validRoles = new[] { "ReadOnly", "Manager", "Admin" };
        if (!validRoles.Contains(request.RoleName))
            return BadRequest(new { message = "Invalid role. Valid roles are: ReadOnly, Manager, Admin" });

        // Get current roles
        var currentRoles = await _userManager.GetRolesAsync(user);

        // Remove from all current roles
        if (currentRoles.Any())
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
                return BadRequest(new { message = "Failed to update user role", errors = removeResult.Errors.Select(e => e.Description) });
        }

        // Add to new role
        var addResult = await _userManager.AddToRoleAsync(user, request.RoleName);
        if (!addResult.Succeeded)
            return BadRequest(new { message = "Failed to assign new role", errors = addResult.Errors.Select(e => e.Description) });

        _logger.LogInformation("User {UserId} role changed to {Role}", id, request.RoleName);

        return Ok(new { message = $"User role updated to {request.RoleName}" });
    }

    /// <summary>
    /// Delete user account
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound(new { message = "User not found" });

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
            return BadRequest(new { message = "Failed to delete user", errors = result.Errors.Select(e => e.Description) });

        _logger.LogInformation("User {UserId} deleted", id);

        return Ok(new { message = "User deleted successfully" });
    }

    /// <summary>
    /// Lock or unlock user account
    /// </summary>
    [HttpPut("{id}/lock")]
    public async Task<IActionResult> LockUser(string id, [FromBody] LockUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound(new { message = "User not found" });

        if (request.Locked)
        {
            // Lock user for 100 years (effectively permanent)
            var result = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
            if (!result.Succeeded)
                return BadRequest(new { message = "Failed to lock user", errors = result.Errors.Select(e => e.Description) });

            _logger.LogInformation("User {UserId} locked", id);
            return Ok(new { message = "User account locked" });
        }
        else
        {
            // Unlock user
            var result = await _userManager.SetLockoutEndDateAsync(user, null);
            if (!result.Succeeded)
                return BadRequest(new { message = "Failed to unlock user", errors = result.Errors.Select(e => e.Description) });

            // Reset failed login count
            await _userManager.ResetAccessFailedCountAsync(user);

            _logger.LogInformation("User {UserId} unlocked", id);
            return Ok(new { message = "User account unlocked" });
        }
    }

    /// <summary>
    /// Update user profile (admin can update any user)
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound(new { message = "User not found" });

        // Update user fields
        if (request.FirstName != null)
            user.FirstName = request.FirstName;

        if (request.LastName != null)
            user.LastName = request.LastName;

        if (request.DateOfBirth != null)
            user.DateOfBirth = request.DateOfBirth;

        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(new { message = "Failed to update user", errors = result.Errors.Select(e => e.Description) });

        _logger.LogInformation("User {UserId} updated by admin", id);

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new UserResponse
        {
            Id = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            DateOfBirth = user.DateOfBirth,
            EmailConfirmed = user.EmailConfirmed,
            Roles = roles.ToList(),
            CreatedAt = user.CreatedAt
        });
    }
}
