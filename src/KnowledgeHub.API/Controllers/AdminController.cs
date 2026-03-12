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

    [HttpGet("stats")]
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

    [HttpGet("users")]
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

    [HttpPut("users/{id:guid}/toggle")]
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
