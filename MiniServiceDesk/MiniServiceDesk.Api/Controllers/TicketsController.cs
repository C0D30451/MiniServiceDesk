using Microsoft.AspNetCore.Authorization;
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

    public TicketsController(AppDbContext db)
    {
        _db = db;
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
}
