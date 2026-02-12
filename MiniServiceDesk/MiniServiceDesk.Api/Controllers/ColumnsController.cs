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
public class ColumnsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public ColumnsController(AppDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<ActionResult<List<TicketColumn>>> GetMyColumns()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        var columns = await _db.TicketColumns
            .AsNoTracking()
            .Where(c => c.OwnerUserId == user.Id)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Id)
            .ToListAsync();

        return Ok(columns);
    }

    [HttpPost]
    public async Task<ActionResult<TicketColumn>> Create(CreateColumnRequest body)
    {
        var name = (body.Name ?? string.Empty).Trim();
        if (name.Length < 1)
        {
            return BadRequest("Column name is required.");
        }

        if (name.Length > 60)
        {
            return BadRequest("Column name too long.");
        }

        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        var alreadyExists = await _db.TicketColumns.AnyAsync(c => c.OwnerUserId == user.Id && c.Name == name);
        if (alreadyExists)
        {
            return Conflict("Column with this name already exists.");
        }

        var maxSort = await _db.TicketColumns
            .Where(c => c.OwnerUserId == user.Id)
            .Select(c => (int?)c.SortOrder)
            .MaxAsync() ?? 0;

        var column = new TicketColumn
        {
            Name = name,
            OwnerUserId = user.Id,
            OwnerUserName = user.UserName ?? string.Empty,
            SortOrder = maxSort + 10,
            CreatedAt = DateTime.UtcNow
        };

        _db.TicketColumns.Add(column);
        await _db.SaveChangesAsync();

        return Ok(column);
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        var column = await _db.TicketColumns.FirstOrDefaultAsync(c => c.Id == id && c.OwnerUserId == user.Id);
        if (column is null)
        {
            return NotFound();
        }

        _db.TicketColumns.Remove(column);
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
