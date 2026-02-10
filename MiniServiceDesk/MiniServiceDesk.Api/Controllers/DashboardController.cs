using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniServiceDesk.Api.Data;
using MiniServiceDesk.Api.Dtos;

namespace MiniServiceDesk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public DashboardController(AppDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryResponse>> GetSummary()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        var currentUserId = user.Id;
        var currentUserName = user.UserName ?? string.Empty;

        var scoped = _db.Tickets.Where(t =>
            // Unassigned reale
            ((t.AssignedToUserName == null || t.AssignedToUserName == string.Empty) &&
             (t.AssignedToUserId == null || t.AssignedToUserId == string.Empty))
            ||
            // Se esiste username, è il campo canonico per dashboard visibility
            (t.AssignedToUserName != null && t.AssignedToUserName != string.Empty && t.AssignedToUserName == currentUserName)
            ||
            // Fallback solo per record legacy senza username ma con id
            ((t.AssignedToUserName == null || t.AssignedToUserName == string.Empty) &&
             t.AssignedToUserId == currentUserId)
        );

        var total = await scoped.CountAsync();
        var unassigned = await scoped.CountAsync(t =>
            (t.AssignedToUserName == null || t.AssignedToUserName == string.Empty) &&
            (t.AssignedToUserId == null || t.AssignedToUserId == string.Empty));

        var byStatusRows = await scoped
            .GroupBy(t => (int)t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var byPriorityRows = await scoped
            .GroupBy(t => (int)t.Priority)
            .Select(g => new { Priority = g.Key, Count = g.Count() })
            .ToListAsync();

        return Ok(new DashboardSummaryResponse
        {
            Total = total,
            Unassigned = unassigned,
            ByStatus = byStatusRows.ToDictionary(x => x.Status, x => x.Count),
            ByPriority = byPriorityRows.ToDictionary(x => x.Priority, x => x.Count)
        });
    }

    [HttpGet("agents")]
    [Authorize(Roles = "Agent,Admin")]
    public async Task<ActionResult<List<AgentWorkloadRow>>> GetAgentsWorkload()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        var currentUserId = user.Id;
        var currentUserName = user.UserName ?? string.Empty;

        var assignedToCurrent = _db.Tickets.Where(t =>
            (t.AssignedToUserName != null && t.AssignedToUserName != string.Empty && t.AssignedToUserName == currentUserName)
            ||
            ((t.AssignedToUserName == null || t.AssignedToUserName == string.Empty) &&
             t.AssignedToUserId == currentUserId));

        var byStatusRows = await assignedToCurrent
            .GroupBy(t => (int)t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var totalAssigned = byStatusRows.Sum(x => x.Count);

        var result = new List<AgentWorkloadRow>
        {
            new()
            {
                AgentUserName = user.UserName ?? string.Empty,
                TotalAssigned = totalAssigned,
                ByStatus = byStatusRows.ToDictionary(x => x.Status, x => x.Count)
            }
        };

        return Ok(result);
    }

    private async Task<IdentityUser?> GetCurrentUserAsync()
    {
        var userName = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
        {
            return null;
        }

        return await _userManager.FindByNameAsync(userName);
    }
}
