using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniServiceDesk.Api.Data;
using MiniServiceDesk.Api.Dtos;
using MiniServiceDesk.Api.models;

namespace MiniServiceDesk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public TicketsController(AppDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<ActionResult<List<Ticket>>> GetAll()
    {
        var tickets = await _db.Tickets
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return Ok(tickets);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Ticket>> GetById(int id)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    [HttpGet("{id:int}/details")]
    public async Task<ActionResult<TicketDetailsResponse>> GetDetails(int id)
    {
        var ticket = await _db.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null)
        {
            return NotFound();
        }

        var comments = await _db.TicketComments
            .AsNoTracking()
            .Where(c => c.TicketId == id)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        return Ok(new TicketDetailsResponse
        {
            Ticket = ticket,
            Comments = comments
        });
    }

    [HttpPost]
    [Authorize(Roles = "User,Agent,Admin")]
    public async Task<ActionResult<Ticket>> Create(CreateTicketRequest body)
    {
        var ticket = new Ticket
        {
            Title = body.Title,
            Description = body.Description,
            Category = body.Category,
            Priority = (TicketPriority)body.Priority,
            Status = TicketStatus.Open,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ticket);
    }

    [HttpPost("{id:int}/comments")]
    [Authorize(Roles = "User,Agent,Admin")]
    public async Task<ActionResult> AddComment(int id, AddCommentRequest body)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null)
        {
            return NotFound();
        }

        if (ticket.Status == TicketStatus.Closed)
        {
            return BadRequest("Closed tickets cannot be modified.");
        }

        var text = (body.Text ?? string.Empty).Trim();
        if (text.Length < 1)
        {
            return BadRequest("Comment is empty.");
        }

        if (text.Length > 2000)
        {
            return BadRequest("Comment too long.");
        }

        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        _db.TicketComments.Add(new TicketComment
        {
            TicketId = id,
            Text = text,
            AuthorUserId = user.Id,
            AuthorUserName = user.UserName ?? "unknown",
            CreatedAt = DateTime.UtcNow
        });

        ticket.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id:int}/assign")]
    [Authorize(Roles = "Agent,Admin")]
    public async Task<ActionResult> Assign(int id, AssignTicketRequest body)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null)
        {
            return NotFound();
        }

        if (ticket.Status == TicketStatus.Closed)
        {
            return BadRequest("Closed tickets cannot be modified.");
        }

        var assigneeName = (body.AssigneeUserName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(assigneeName))
        {
            return BadRequest("AssigneeUserName is required.");
        }

        var assignee = await _userManager.FindByNameAsync(assigneeName);
        if (assignee is null)
        {
            return BadRequest("User not found.");
        }

        var roles = await _userManager.GetRolesAsync(assignee);
        if (!roles.Contains("Agent") && !roles.Contains("Admin"))
        {
            return BadRequest("Assignee must be Agent or Admin.");
        }

        ticket.AssignedToUserId = assignee.Id;
        ticket.AssignedToUserName = assignee.UserName;
        ticket.AssignedAt = DateTime.UtcNow;
        ticket.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/status")]
    [Authorize(Roles = "Agent,Admin")]
    public async Task<ActionResult> ChangeStatus(int id, ChangeStatusRequest body)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null)
        {
            return NotFound();
        }

        if (ticket.Status == TicketStatus.Closed)
        {
            return BadRequest("Closed tickets cannot be modified.");
        }

        if (!Enum.IsDefined(typeof(TicketStatus), body.NewStatus))
        {
            return BadRequest("Invalid status.");
        }

        var newStatus = (TicketStatus)body.NewStatus;

        if (ticket.Priority == TicketPriority.Critical && newStatus == TicketStatus.Waiting)
        {
            var reason = (body.Comment ?? string.Empty).Trim();
            if (reason.Length < 5)
            {
                return BadRequest("Critical tickets require a comment to move to Waiting.");
            }

            var currentUser = await GetCurrentUserAsync();
            if (currentUser is null)
            {
                return Unauthorized();
            }

            _db.TicketComments.Add(new TicketComment
            {
                TicketId = id,
                Text = $"[Status change reason] {reason}",
                AuthorUserId = currentUser.Id,
                AuthorUserName = currentUser.UserName ?? "unknown",
                CreatedAt = DateTime.UtcNow
            });
        }

        ticket.Status = newStatus;
        ticket.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

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
