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
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        var currentUserId = user.Id;
        var currentUserName = user.UserName ?? string.Empty;
        var isAgentOrAdmin = User.IsInRole("Agent") || User.IsInRole("Admin");

        IQueryable<Ticket> visibleQuery = _db.Tickets.AsNoTracking();
        if (isAgentOrAdmin)
        {
            visibleQuery = visibleQuery.Where(t =>
                (
                    (t.CreatedByUserId != null && t.CreatedByUserId != string.Empty && t.CreatedByUserId == currentUserId)
                    ||
                    ((t.CreatedByUserId == null || t.CreatedByUserId == string.Empty) &&
                     t.CreatedByUserName != null && t.CreatedByUserName != string.Empty &&
                     t.CreatedByUserName == currentUserName)
                )
                ||
                (
                    (t.AssignedToUserName != null && t.AssignedToUserName != string.Empty &&
                     t.AssignedToUserName == currentUserName)
                    ||
                    ((t.AssignedToUserName == null || t.AssignedToUserName == string.Empty) &&
                     t.AssignedToUserId != null && t.AssignedToUserId != string.Empty &&
                     t.AssignedToUserId == currentUserId)
                )
                ||
                ((t.AssignedToUserName == null || t.AssignedToUserName == string.Empty) &&
                 (t.AssignedToUserId == null || t.AssignedToUserId == string.Empty)));
        }
        else
        {
            visibleQuery = visibleQuery.Where(t =>
                (t.CreatedByUserId != null && t.CreatedByUserId != string.Empty && t.CreatedByUserId == currentUserId)
                ||
                ((t.CreatedByUserId == null || t.CreatedByUserId == string.Empty) &&
                 t.CreatedByUserName != null && t.CreatedByUserName != string.Empty &&
                 t.CreatedByUserName == currentUserName));
        }

        var tickets = await visibleQuery
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        // Board columns are user-owned; tickets linked to foreign columns must appear in Inbox.
        var myColumnIds = await _db.TicketColumns
            .AsNoTracking()
            .Where(c => c.OwnerUserId == currentUserId)
            .Select(c => c.Id)
            .ToListAsync();
        var myColumnSet = myColumnIds.ToHashSet();

        foreach (var ticket in tickets)
        {
            if (ticket.TicketColumnId is not null && !myColumnSet.Contains(ticket.TicketColumnId.Value))
            {
                ticket.TicketColumnId = null;
            }
        }

        return Ok(tickets);
    }

    [HttpGet("all")]
    [Authorize(Roles = "Agent,Admin")]
    public async Task<ActionResult<TicketListResponse>> GetAllGlobal([FromQuery] TicketListQuery q)
    {
        var page = q.Page < 1 ? 1 : q.Page;
        var pageSize = q.PageSize < 10 ? 10 : (q.PageSize > 100 ? 100 : q.PageSize);

        IQueryable<Ticket> query = _db.Tickets
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(t =>
                t.Title.Contains(s) ||
                t.Description.Contains(s) ||
                t.Category.Contains(s));
        }

        if (q.Status is not null)
        {
            query = query.Where(t => (int)t.Status == q.Status.Value);
        }

        if (q.Priority is not null)
        {
            query = query.Where(t => (int)t.Priority == q.Priority.Value);
        }

        if (q.UnassignedOnly == true)
        {
            query = query.Where(t =>
                (t.AssignedToUserName == null || t.AssignedToUserName == string.Empty) &&
                (t.AssignedToUserId == null || t.AssignedToUserId == string.Empty));
        }

        if (!string.IsNullOrWhiteSpace(q.AssignedTo))
        {
            var userName = q.AssignedTo.Trim();
            query = query.Where(t => t.AssignedToUserName == userName);
        }

        if (q.CreatedFrom is not null)
        {
            query = query.Where(t => t.CreatedAt >= q.CreatedFrom.Value);
        }

        if (q.CreatedTo is not null)
        {
            query = query.Where(t => t.CreatedAt <= q.CreatedTo.Value);
        }

        var sort = (q.Sort ?? "created_desc").Trim().ToLowerInvariant();
        query = sort switch
        {
            "updated_desc" => query.OrderByDescending(t => t.UpdatedAt),
            "priority_desc" => query.OrderByDescending(t => (int)t.Priority).ThenByDescending(t => t.UpdatedAt),
            _ => query.OrderByDescending(t => t.CreatedAt)
        };

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new TicketListResponse
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Ticket>> GetById(int id)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        var ticket = await _db.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null)
        {
            return NotFound();
        }

        if (!CanAccessDetails(user, ticket))
        {
            return Forbid();
        }

        return Ok(ticket);
    }

    [HttpGet("{id:int}/details")]
    public async Task<ActionResult<TicketDetailsResponse>> GetDetails(int id)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        var ticket = await _db.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null)
        {
            return NotFound();
        }

        if (!CanAccessDetails(user, ticket))
        {
            return Forbid();
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

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Delete(int id)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null)
        {
            return NotFound();
        }

        _db.Tickets.Remove(ticket);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost]
    [Authorize(Roles = "User,Agent,Admin")]
    public async Task<ActionResult<Ticket>> Create(CreateTicketRequest body)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser is null)
        {
            return Unauthorized();
        }

        var inboxMaxSort = await _db.Tickets
            .Where(t => t.TicketColumnId == null)
            .Select(t => (int?)t.SortOrderInColumn)
            .MaxAsync() ?? 0;

        var ticket = new Ticket
        {
            Title = body.Title,
            Description = body.Description,
            Category = body.Category,
            Priority = (TicketPriority)body.Priority,
            Status = TicketStatus.Open,
            CreatedByUserId = currentUser.Id,
            CreatedByUserName = currentUser.UserName,
            SortOrderInColumn = inboxMaxSort + 10,
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

        if (!CanAccessDetails(user, ticket))
        {
            return Forbid();
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
        if (!roles.Contains("Agent") && !roles.Contains("Admin") && !roles.Contains("User"))
        {
            return BadRequest("Assignee must be User, Agent or Admin.");
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

    [HttpPost("reorder")]
    [Authorize(Roles = "User,Agent,Admin")]
    public async Task<ActionResult> Reorder(ReorderColumnRequest body)
    {
        if (body.OrderedTicketIds is null || body.OrderedTicketIds.Count == 0)
        {
            return BadRequest("No tickets provided.");
        }

        var distinctIds = body.OrderedTicketIds.Distinct().ToList();
        if (distinctIds.Count != body.OrderedTicketIds.Count)
        {
            return BadRequest("Duplicate ticket ids are not allowed.");
        }

        var currentUser = await GetCurrentUserAsync();
        if (currentUser is null)
        {
            return Unauthorized();
        }

        if (body.TicketColumnId is not null)
        {
            var ownsColumn = await _db.TicketColumns.AnyAsync(c =>
                c.Id == body.TicketColumnId.Value &&
                c.OwnerUserId == currentUser.Id);

            if (!ownsColumn)
            {
                return BadRequest("Invalid column.");
            }
        }

        var tickets = await _db.Tickets
            .Where(t => distinctIds.Contains(t.Id))
            .ToListAsync();

        if (tickets.Count != distinctIds.Count)
        {
            return BadRequest("Some tickets were not found.");
        }

        foreach (var ticket in tickets)
        {
            if (!CanAccessBoardTicket(currentUser, ticket))
            {
                return Forbid();
            }

            if (ticket.TicketColumnId != body.TicketColumnId)
            {
                return BadRequest("Ticket list must belong to the same column.");
            }

            if (ticket.Status == TicketStatus.Closed)
            {
                return BadRequest("Closed tickets cannot be reordered.");
            }
        }

        for (var i = 0; i < body.OrderedTicketIds.Count; i++)
        {
            var ticketId = body.OrderedTicketIds[i];
            var ticket = tickets.First(t => t.Id == ticketId);

            ticket.SortOrderInColumn = (i + 1) * 10;
            ticket.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id:int}/move")]
    [Authorize(Roles = "User,Agent,Admin")]
    public async Task<ActionResult> MoveTicket(int id, MoveTicketRequest body)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser is null)
        {
            return Unauthorized();
        }

        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null)
        {
            return NotFound();
        }

        if (!CanAccessBoardTicket(currentUser, ticket))
        {
            return Forbid();
        }

        if (ticket.Status == TicketStatus.Closed)
        {
            return BadRequest("Closed tickets cannot be modified.");
        }

        if (body.TicketColumnId is not null)
        {
            var isValidColumn = await _db.TicketColumns.AnyAsync(c =>
                c.Id == body.TicketColumnId.Value &&
                c.OwnerUserId == currentUser.Id);

            if (!isValidColumn)
            {
                return BadRequest("Invalid column.");
            }
        }

        if (ticket.TicketColumnId != body.TicketColumnId)
        {
            var maxSort = await _db.Tickets
                .Where(t => t.TicketColumnId == body.TicketColumnId)
                .Select(t => (int?)t.SortOrderInColumn)
                .MaxAsync() ?? 0;

            ticket.SortOrderInColumn = maxSort + 10;
        }

        ticket.TicketColumnId = body.TicketColumnId;
        ticket.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private bool CanAccessBoardTicket(IdentityUser currentUser, Ticket ticket)
    {
        var currentUserName = currentUser.UserName ?? string.Empty;
        var isCreatedByCurrent = MatchesUser(ticket.CreatedByUserId, ticket.CreatedByUserName, currentUser.Id, currentUserName);
        if (!IsAgentOrAdmin())
        {
            return isCreatedByCurrent;
        }

        var isAssignedToCurrent = MatchesUser(ticket.AssignedToUserId, ticket.AssignedToUserName, currentUser.Id, currentUserName);
        var isUnassigned = string.IsNullOrWhiteSpace(ticket.AssignedToUserId) &&
                           string.IsNullOrWhiteSpace(ticket.AssignedToUserName);
        return isCreatedByCurrent || isAssignedToCurrent || isUnassigned;
    }

    private bool CanAccessDetails(IdentityUser currentUser, Ticket ticket)
    {
        if (IsAgentOrAdmin())
        {
            // Agent/Admin can open any ticket from the global list.
            return true;
        }

        var currentUserName = currentUser.UserName ?? string.Empty;
        return MatchesUser(ticket.CreatedByUserId, ticket.CreatedByUserName, currentUser.Id, currentUserName);
    }

    private static bool MatchesUser(string? candidateUserId, string? candidateUserName, string currentUserId, string currentUserName)
    {
        if (!string.IsNullOrWhiteSpace(candidateUserId))
        {
            return candidateUserId == currentUserId;
        }

        if (!string.IsNullOrWhiteSpace(candidateUserName))
        {
            return string.Equals(candidateUserName, currentUserName, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private bool IsAgentOrAdmin() => User.IsInRole("Agent") || User.IsInRole("Admin");

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
