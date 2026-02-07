// MiniServiceDesk.Api/Controllers/TicketsController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniServiceDesk.Api.Data;
using MiniServiceDesk.Api.models;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using MiniServiceDesk.Api.Dtos;
using System.Data.Common;
namespace MiniServiceDesk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]

//classe controller, eredita da controllerbase
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
        if (ticket is null) return NotFound();

        return Ok(ticket);
    }

    [HttpPost]
    public async Task<ActionResult<Ticket>> Create(CreateTicketRequest body)
    {
        var input = new Ticket
        {
            Title = body.Title,
            Description = body.Description,
            Category = body.Category,
            Priority = (TicketPriority)body.Priority,
            Status = TicketStatus.Open,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Tickets.Add(input);
        {
            input.Id = 0;
            input.CreatedAt = DateTime.UtcNow;
            input.UpdatedAt = DateTime.UtcNow;
            input.Status = TicketStatus.Open;

            _db.Tickets.Add(input);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = input.Id }, input);
        }
    }
}
