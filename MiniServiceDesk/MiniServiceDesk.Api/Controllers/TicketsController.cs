using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniServiceDesk.Api.Data;
using MiniServiceDesk.Api.Dtos;
using MiniServiceDesk.Api.models;
using MiniServiceDesk.Api.Services;
using System.Text;

namespace MiniServiceDesk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TicketsController : ControllerBase
{
    private const long MaxAttachmentSizeBytes = 10 * 1024 * 1024; // 10 MB

    private readonly AppDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<TicketsController> _logger;
    private readonly IEmailNotificationService _emailNotificationService;

    public TicketsController(
        AppDbContext db,
        UserManager<IdentityUser> userManager,
        IWebHostEnvironment env,
        ILogger<TicketsController> logger,
        IEmailNotificationService emailNotificationService)
    {
        _db = db;
        _userManager = userManager;
        _env = env;
        _logger = logger;
        _emailNotificationService = emailNotificationService;
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
            "urgency_desc" => query
                .OrderBy(t => t.DueAt == null ? 1 : 0)
                .ThenBy(t => t.DueAt)
                .ThenByDescending(t => (int)t.Priority)
                .ThenByDescending(t => t.UpdatedAt),
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

        var events = await _db.TicketEvents
            .AsNoTracking()
            .Where(e => e.TicketId == id)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new TicketEventRow
            {
                Id = e.Id,
                EventType = e.EventType,
                Message = e.Message,
                ActorUserName = e.ActorUserName,
                CreatedAt = e.CreatedAt
            })
            .ToListAsync();

        var attachments = await _db.TicketAttachments
            .AsNoTracking()
            .Where(a => a.TicketId == id)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new TicketAttachmentRow
            {
                Id = a.Id,
                OriginalFileName = a.OriginalFileName,
                ContentType = a.ContentType,
                FileSizeBytes = a.FileSizeBytes,
                UploadedByUserName = a.UploadedByUserName,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();

        return Ok(new TicketDetailsResponse
        {
            Ticket = ticket,
            Comments = comments,
            Events = events,
            Attachments = attachments
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

        var attachmentPaths = await _db.TicketAttachments
            .AsNoTracking()
            .Where(a => a.TicketId == id)
            .Select(a => a.StoredRelativePath)
            .ToListAsync();

        _db.Tickets.Remove(ticket);
        await _db.SaveChangesAsync();

        foreach (var relativePath in attachmentPaths)
        {
            TryDeleteStoredFile(relativePath);
        }

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
            DueAt = ResolveDueAt(body.DueAt, (TicketPriority)body.Priority),
            SortOrderInColumn = inboxMaxSort + 10,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();

        _db.TicketEvents.Add(new TicketEvent
        {
            TicketId = ticket.Id,
            EventType = "ticket_created",
            Message = $"Ticket creato da {currentUser.UserName}.",
            ActorUserId = currentUser.Id,
            ActorUserName = currentUser.UserName,
            CreatedAt = DateTime.UtcNow
        });
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

        _db.TicketEvents.Add(new TicketEvent
        {
            TicketId = ticket.Id,
            EventType = "comment_added",
            Message = $"{user.UserName} ha aggiunto un commento.",
            ActorUserId = user.Id,
            ActorUserName = user.UserName,
            CreatedAt = DateTime.UtcNow
        });

        await AddNotificationForInterestedUsersAsync(
            ticket,
            user,
            "comment_added",
            $"{user.UserName} ha commentato il ticket #{ticket.Id}.");

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

        var actor = await GetCurrentUserAsync();
        var actorName = actor?.UserName ?? "system";
        var actorId = actor?.Id;

        _db.TicketEvents.Add(new TicketEvent
        {
            TicketId = ticket.Id,
            EventType = "assignment_changed",
            Message = $"Ticket assegnato a {assignee.UserName} da {actorName}.",
            ActorUserId = actorId,
            ActorUserName = actorName,
            CreatedAt = DateTime.UtcNow
        });

        await CreateNotificationAsync(
            assignee.Id,
            assignee.UserName ?? assigneeName,
            ticket.Id,
            "ticket_assigned",
            $"Sei stato assegnato al ticket #{ticket.Id}.");

        await SendEmailToUserByIdAsync(
            assignee.Id,
            $"[MiniServiceDesk] Ticket #{ticket.Id} assegnato",
            $"Ciao {assignee.UserName},\n\nsei stato assegnato al ticket #{ticket.Id} ({ticket.Title}).\nStato attuale: {ticket.Status}.\n\nMiniServiceDesk");

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
        var oldStatus = ticket.Status;

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

        var actor = await GetCurrentUserAsync();
        _db.TicketEvents.Add(new TicketEvent
        {
            TicketId = ticket.Id,
            EventType = "status_changed",
            Message = $"Stato cambiato da {oldStatus} a {newStatus}.",
            ActorUserId = actor?.Id,
            ActorUserName = actor?.UserName ?? "system",
            CreatedAt = DateTime.UtcNow
        });

        if (actor is not null)
        {
            await AddNotificationForInterestedUsersAsync(
                ticket,
                actor,
                "status_changed",
                $"Lo stato del ticket #{ticket.Id} e' ora {newStatus}.",
                sendEmail: true,
                emailSubject: $"[MiniServiceDesk] Ticket #{ticket.Id} aggiornato",
                emailBody: $"Il ticket #{ticket.Id} ({ticket.Title}) e' passato da {oldStatus} a {newStatus}.");
        }

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

        var previousColumnId = ticket.TicketColumnId;
        ticket.TicketColumnId = body.TicketColumnId;
        ticket.UpdatedAt = DateTime.UtcNow;

        _db.TicketEvents.Add(new TicketEvent
        {
            TicketId = ticket.Id,
            EventType = "column_moved",
            Message = $"Spostato da colonna {(previousColumnId?.ToString() ?? "Inbox")} a {(body.TicketColumnId?.ToString() ?? "Inbox")}.",
            ActorUserId = currentUser.Id,
            ActorUserName = currentUser.UserName,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPatch("{id:int}/due")]
    [Authorize(Roles = "Agent,Admin")]
    public async Task<ActionResult> SetDueDate(int id, SetDueDateRequest body)
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

        var actor = await GetCurrentUserAsync();
        if (actor is null)
        {
            return Unauthorized();
        }

        var dueAt = body.DueAt?.ToUniversalTime();
        if (dueAt is not null && dueAt.Value < ticket.CreatedAt.AddMinutes(-1))
        {
            return BadRequest("Due date cannot be before ticket creation.");
        }

        var oldDueAt = ticket.DueAt;
        ticket.DueAt = dueAt;
        ticket.UpdatedAt = DateTime.UtcNow;

        var oldDueText = oldDueAt?.ToString("u") ?? "none";
        var newDueText = dueAt?.ToString("u") ?? "none";
        _db.TicketEvents.Add(new TicketEvent
        {
            TicketId = ticket.Id,
            EventType = "due_date_changed",
            Message = $"Due date cambiata da {oldDueText} a {newDueText}.",
            ActorUserId = actor.Id,
            ActorUserName = actor.UserName,
            CreatedAt = DateTime.UtcNow
        });

        await AddNotificationForInterestedUsersAsync(
            ticket,
            actor,
            "due_date_changed",
            $"La scadenza del ticket #{ticket.Id} e' stata aggiornata.");

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:int}/attachments")]
    public async Task<ActionResult<List<TicketAttachmentRow>>> GetAttachments(int id)
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

        var attachments = await _db.TicketAttachments
            .AsNoTracking()
            .Where(a => a.TicketId == id)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new TicketAttachmentRow
            {
                Id = a.Id,
                OriginalFileName = a.OriginalFileName,
                ContentType = a.ContentType,
                FileSizeBytes = a.FileSizeBytes,
                UploadedByUserName = a.UploadedByUserName,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();

        return Ok(attachments);
    }

    [HttpPost("{id:int}/attachments")]
    [Authorize(Roles = "User,Agent,Admin")]
    [RequestSizeLimit(MaxAttachmentSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxAttachmentSizeBytes)]
    public async Task<ActionResult<TicketAttachmentRow>> UploadAttachment(int id, IFormFile file)
    {
        var actor = await GetCurrentUserAsync();
        if (actor is null)
        {
            return Unauthorized();
        }

        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null)
        {
            return NotFound();
        }

        if (!CanAccessDetails(actor, ticket))
        {
            return Forbid();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest("File is required.");
        }

        if (file.Length > MaxAttachmentSizeBytes)
        {
            return BadRequest("File too large. Max size is 10 MB.");
        }

        var sanitizedOriginalName = SanitizeFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(sanitizedOriginalName))
        {
            return BadRequest("Invalid file name.");
        }

        var extension = Path.GetExtension(sanitizedOriginalName).ToLowerInvariant();
        if (!IsAllowedAttachmentExtension(extension))
        {
            return BadRequest("File type not allowed.");
        }

        var relativeDirectory = Path.Combine("ticket-attachments", id.ToString());
        var absoluteDirectory = Path.Combine(_env.ContentRootPath, relativeDirectory);
        Directory.CreateDirectory(absoluteDirectory);

        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var absoluteFilePath = Path.Combine(absoluteDirectory, storedFileName);

        try
        {
            await using (var stream = System.IO.File.Create(absoluteFilePath))
            {
                await file.CopyToAsync(stream);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while saving attachment for ticket {TicketId}", id);
            return StatusCode(500, "Unable to store attachment.");
        }

        var storedRelativePath = Path.Combine(relativeDirectory, storedFileName).Replace("\\", "/");
        var attachment = new TicketAttachment
        {
            TicketId = ticket.Id,
            OriginalFileName = sanitizedOriginalName,
            StoredFileName = storedFileName,
            StoredRelativePath = storedRelativePath,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType,
            FileSizeBytes = file.Length,
            UploadedByUserId = actor.Id,
            UploadedByUserName = actor.UserName,
            CreatedAt = DateTime.UtcNow
        };
        _db.TicketAttachments.Add(attachment);

        _db.TicketEvents.Add(new TicketEvent
        {
            TicketId = ticket.Id,
            EventType = "attachment_uploaded",
            Message = $"{actor.UserName} ha caricato il file {sanitizedOriginalName}.",
            ActorUserId = actor.Id,
            ActorUserName = actor.UserName,
            CreatedAt = DateTime.UtcNow
        });

        await AddNotificationForInterestedUsersAsync(
            ticket,
            actor,
            "attachment_uploaded",
            $"{actor.UserName} ha aggiunto un allegato al ticket #{ticket.Id}.");

        ticket.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new TicketAttachmentRow
        {
            Id = attachment.Id,
            OriginalFileName = attachment.OriginalFileName,
            ContentType = attachment.ContentType,
            FileSizeBytes = attachment.FileSizeBytes,
            UploadedByUserName = attachment.UploadedByUserName,
            CreatedAt = attachment.CreatedAt
        });
    }

    [HttpGet("{id:int}/attachments/{attachmentId:int}/download")]
    public async Task<ActionResult> DownloadAttachment(int id, int attachmentId)
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

        var attachment = await _db.TicketAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.TicketId == id && a.Id == attachmentId);
        if (attachment is null)
        {
            return NotFound();
        }

        var absolutePath = Path.Combine(_env.ContentRootPath, attachment.StoredRelativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
        if (!System.IO.File.Exists(absolutePath))
        {
            return NotFound("Attachment file not found on disk.");
        }

        return PhysicalFile(absolutePath, attachment.ContentType, attachment.OriginalFileName);
    }

    private static DateTime ResolveDueAt(DateTime? dueAtFromRequest, TicketPriority priority)
    {
        if (dueAtFromRequest is not null)
        {
            return dueAtFromRequest.Value.ToUniversalTime();
        }

        return DateTime.UtcNow.AddDays(14);
    }

    private async Task AddNotificationForInterestedUsersAsync(
        Ticket ticket,
        IdentityUser actor,
        string notificationType,
        string message,
        bool sendEmail = false,
        string? emailSubject = null,
        string? emailBody = null)
    {
        var recipients = new List<(string? UserId, string? UserName)>
        {
            (ticket.CreatedByUserId, ticket.CreatedByUserName),
            (ticket.AssignedToUserId, ticket.AssignedToUserName)
        };

        var seenRecipientIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var recipient in recipients)
        {
            var resolved = await ResolveUserDestinationAsync(recipient.UserId, recipient.UserName);
            if (resolved.UserId is null || resolved.UserName is null)
            {
                continue;
            }

            if (resolved.UserId == actor.Id)
            {
                continue;
            }

            if (!seenRecipientIds.Add(resolved.UserId))
            {
                continue;
            }

            await CreateNotificationAsync(
                resolved.UserId,
                resolved.UserName,
                ticket.Id,
                notificationType,
                message);

            if (sendEmail)
            {
                await SendEmailToUserByIdAsync(
                    resolved.UserId,
                    emailSubject ?? "[MiniServiceDesk] Ticket notification",
                    emailBody ?? message);
            }
        }
    }

    private async Task<(string? UserId, string? UserName)> ResolveUserDestinationAsync(string? userId, string? userName)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                var byId = await _userManager.FindByIdAsync(userId);
                return (byId?.Id, byId?.UserName);
            }

            return (userId, userName);
        }

        if (!string.IsNullOrWhiteSpace(userName))
        {
            var byName = await _userManager.FindByNameAsync(userName);
            if (byName is not null)
            {
                return (byName.Id, byName.UserName);
            }
        }

        return (null, null);
    }

    private Task CreateNotificationAsync(
        string userId,
        string userName,
        int ticketId,
        string notificationType,
        string message)
    {
        _db.UserNotifications.Add(new UserNotification
        {
            UserId = userId,
            UserName = userName,
            TicketId = ticketId,
            NotificationType = notificationType,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

        return Task.CompletedTask;
    }

    private async Task SendEmailToUserByIdAsync(string userId, string subject, string body)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        await _emailNotificationService.SendAsync(user.Email, subject, body);
    }

    private static string SanitizeFileName(string originalName)
    {
        if (string.IsNullOrWhiteSpace(originalName))
        {
            return string.Empty;
        }

        var fileName = Path.GetFileName(originalName);
        var invalidChars = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(fileName.Length);
        foreach (var ch in fileName)
        {
            sb.Append(invalidChars.Contains(ch) ? '_' : ch);
        }

        return sb.ToString();
    }

    private static bool IsAllowedAttachmentExtension(string extension)
    {
        return extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".pdf" or ".txt" or ".log" or ".csv" or ".json";
    }

    private void TryDeleteStoredFile(string? storedRelativePath)
    {
        if (string.IsNullOrWhiteSpace(storedRelativePath))
        {
            return;
        }

        try
        {
            var absolutePath = Path.Combine(_env.ContentRootPath, storedRelativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
            if (System.IO.File.Exists(absolutePath))
            {
                System.IO.File.Delete(absolutePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to delete attachment file {StoredRelativePath}", storedRelativePath);
        }
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
