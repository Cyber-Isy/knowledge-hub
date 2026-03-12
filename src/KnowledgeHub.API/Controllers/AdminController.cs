using Asp.Versioning;
using KnowledgeHub.API.DTOs;
using KnowledgeHub.Core.Entities;
using KnowledgeHub.Core.Interfaces.Repositories;
using KnowledgeHub.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeHub.API.Controllers;

/// <summary>
/// Administrative endpoints for system management. Requires the Admin role.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/admin")]
[ApiVersion("1.0")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public AdminController(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    /// <summary>
    /// Returns aggregate platform statistics including user, document, and conversation counts.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Platform-wide statistics.</returns>
    /// <response code="200">Returns the admin statistics.</response>
    /// <response code="401">The request is not authenticated.</response>
    /// <response code="403">The user does not have the Admin role.</response>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(AdminStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminStatsDto>> GetStats(CancellationToken ct)
    {
        var totalUsers = await _userManager.Users.CountAsync(ct);
        var totalDocuments = await _context.Documents.CountAsync(ct);
        var totalConversations = await _context.Conversations.CountAsync(ct);
        var totalMessages = await _context.Messages.CountAsync(ct);
        var totalTokensUsed = await _context.Messages.SumAsync(m => (long)m.TokensUsed, ct);

        return Ok(new AdminStatsDto
        {
            TotalUsers = totalUsers,
            TotalDocuments = totalDocuments,
            TotalConversations = totalConversations,
            TotalMessages = totalMessages,
            TotalTokensUsed = totalTokensUsed
        });
    }

    /// <summary>
    /// Returns a list of all registered users with their roles and usage statistics.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of all users.</returns>
    /// <response code="200">Returns the user list.</response>
    /// <response code="401">The request is not authenticated.</response>
    /// <response code="403">The user does not have the Admin role.</response>
    [HttpGet("users")]
    [ProducesResponseType(typeof(IEnumerable<AdminUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<AdminUserDto>>> GetUsers(CancellationToken ct)
    {
        var users = await _userManager.Users.ToListAsync(ct);
        var result = new List<AdminUserDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var docCount = await _context.Documents.CountAsync(d => d.UserId == user.Id, ct);
            var convoCount = await _context.Conversations.CountAsync(c => c.UserId == user.Id, ct);

            result.Add(new AdminUserDto
            {
                Id = user.Id,
                Email = user.Email!,
                DisplayName = user.DisplayName,
                CreatedAt = user.CreatedAt,
                IsEnabled = !await _userManager.IsLockedOutAsync(user) && user.LockoutEnd is null,
                DocumentCount = docCount,
                ConversationCount = convoCount,
                Roles = roles.ToList()
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Toggles a user's enabled/disabled status. Disabled users cannot log in.
    /// </summary>
    /// <param name="id">The user ID to toggle.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">User status toggled successfully.</response>
    /// <response code="401">The request is not authenticated.</response>
    /// <response code="403">The user does not have the Admin role.</response>
    /// <response code="404">User not found.</response>
    [HttpPut("users/{id:guid}/toggle")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleUser(Guid id, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return NotFound();

        if (user.LockoutEnd is not null && user.LockoutEnd > DateTimeOffset.UtcNow)
        {
            // Re-enable user
            await _userManager.SetLockoutEndDateAsync(user, null);
        }
        else
        {
            // Disable user
            await _userManager.SetLockoutEnabledAsync(user, true);
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        }

        return NoContent();
    }
}
