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
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public NotificationsController(AppDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<ActionResult<List<NotificationRow>>> GetMine([FromQuery] bool unreadOnly = false)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        var query = _db.UserNotifications
            .AsNoTracking()
            .Where(n => n.UserId == user.Id);

        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        var rows = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(100)
            .Select(n => new NotificationRow
            {
                Id = n.Id,
                TicketId = n.TicketId,
                Message = n.Message,
                NotificationType = n.NotificationType,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();

        return Ok(rows);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountResponse>> GetUnreadCount()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        var count = await _db.UserNotifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == user.Id && !n.IsRead);

        return Ok(new UnreadCountResponse { Count = count });
    }

    [HttpPost("{id:int}/read")]
    public async Task<ActionResult> MarkAsRead(int id)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        var notification = await _db.UserNotifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == user.Id);
        if (notification is null)
        {
            return NotFound();
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<ActionResult> MarkAllAsRead()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        var toUpdate = await _db.UserNotifications
            .Where(n => n.UserId == user.Id && !n.IsRead)
            .ToListAsync();

        foreach (var notification in toUpdate)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
        }

        if (toUpdate.Count > 0)
        {
            await _db.SaveChangesAsync();
        }

        return NoContent();
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
