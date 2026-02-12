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
public class UsersController : ControllerBase
{
    private static readonly string[] AllowedRoles = ["User", "Agent", "Admin"];

    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AppDbContext _db;

    public UsersController(
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        AppDbContext db)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
    }

    [HttpGet("agents")]
    [Authorize(Roles = "Agent,Admin")]
    public async Task<ActionResult<List<string>>> GetAssignableAgents()
    {
        var agents = await _userManager.GetUsersInRoleAsync("Agent");
        var admins = await _userManager.GetUsersInRoleAsync("Admin");
        var users = await _userManager.GetUsersInRoleAsync("User");

        var userNames = agents
            .Concat(admins)
            .Concat(users)
            .Select(u => u.UserName ?? string.Empty)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(u => u, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(userNames);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<UserListRow>>> GetAllUsers()
    {
        var users = await _userManager.Users
            .AsNoTracking()
            .OrderBy(u => u.UserName)
            .ToListAsync();

        var rows = new List<UserListRow>(users.Count);
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            rows.Add(new UserListRow
            {
                UserName = user.UserName ?? string.Empty,
                Roles = roles.OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList()
            });
        }

        return Ok(rows);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserListRow>> CreateUser(CreateUserRequest body)
    {
        var userName = (body.UserName ?? string.Empty).Trim();
        var password = (body.Password ?? string.Empty).Trim();
        var role = (body.Role ?? string.Empty).Trim();

        if (userName.Length < 3 || password.Length < 8 || !IsAllowedRole(role))
        {
            return BadRequest("Invalid payload.");
        }

        var existingUser = await _userManager.FindByNameAsync(userName);
        if (existingUser is not null)
        {
            return Conflict("User already exists.");
        }

        if (!await _roleManager.RoleExistsAsync(role))
        {
            return BadRequest("Role not found.");
        }

        var user = new IdentityUser { UserName = userName };
        var createResult = await _userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            return BadRequest(FormatIdentityErrors(createResult.Errors));
        }

        var addRoleResult = await _userManager.AddToRoleAsync(user, role);
        if (!addRoleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return BadRequest(FormatIdentityErrors(addRoleResult.Errors));
        }

        return Ok(new UserListRow
        {
            UserName = user.UserName ?? userName,
            Roles = [role]
        });
    }

    [HttpPut("{userName}/role")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserListRow>> ChangeUserRole(string userName, UpdateUserRoleRequest body)
    {
        var targetUserName = (userName ?? string.Empty).Trim();
        var newRole = (body.Role ?? string.Empty).Trim();

        if (targetUserName.Length < 1 || !IsAllowedRole(newRole))
        {
            return BadRequest("Invalid payload.");
        }

        if (!await _roleManager.RoleExistsAsync(newRole))
        {
            return BadRequest("Role not found.");
        }

        var user = await _userManager.FindByNameAsync(targetUserName);
        if (user is null)
        {
            return NotFound();
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var rolesToRemove = currentRoles
            .Where(IsAllowedRole)
            .ToList();

        if (rolesToRemove.Count > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeResult.Succeeded)
            {
                return BadRequest(FormatIdentityErrors(removeResult.Errors));
            }
        }

        var addRoleResult = await _userManager.AddToRoleAsync(user, newRole);
        if (!addRoleResult.Succeeded)
        {
            return BadRequest(FormatIdentityErrors(addRoleResult.Errors));
        }

        return Ok(new UserListRow
        {
            UserName = user.UserName ?? targetUserName,
            Roles = [newRole]
        });
    }

    [HttpDelete("{userName}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteUser(string userName)
    {
        var targetUserName = (userName ?? string.Empty).Trim();
        if (targetUserName.Length < 1)
        {
            return BadRequest("UserName is required.");
        }

        var currentUserName = User.Identity?.Name ?? string.Empty;
        if (string.Equals(currentUserName, targetUserName, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("You cannot delete your own account.");
        }

        var user = await _userManager.FindByNameAsync(targetUserName);
        if (user is null)
        {
            return NotFound();
        }

        var targetUserId = user.Id;
        var safeTargetUserName = user.UserName ?? targetUserName;

        var assignedTickets = await _db.Tickets
            .Where(t => t.AssignedToUserId == targetUserId || t.AssignedToUserName == safeTargetUserName)
            .ToListAsync();

        foreach (var ticket in assignedTickets)
        {
            ticket.AssignedToUserId = null;
            ticket.AssignedToUserName = null;
            ticket.AssignedAt = null;
            ticket.UpdatedAt = DateTime.UtcNow;
        }

        var ownedColumns = await _db.TicketColumns
            .Where(c => c.OwnerUserId == targetUserId)
            .ToListAsync();

        if (ownedColumns.Count > 0)
        {
            var ownedColumnIds = ownedColumns.Select(c => c.Id).ToList();
            var ticketsInOwnedColumns = await _db.Tickets
                .Where(t => t.TicketColumnId != null && ownedColumnIds.Contains(t.TicketColumnId.Value))
                .ToListAsync();

            foreach (var ticket in ticketsInOwnedColumns)
            {
                ticket.TicketColumnId = null;
                ticket.UpdatedAt = DateTime.UtcNow;
            }

            _db.TicketColumns.RemoveRange(ownedColumns);
        }

        await _db.SaveChangesAsync();

        var deleteResult = await _userManager.DeleteAsync(user);
        if (!deleteResult.Succeeded)
        {
            return BadRequest(FormatIdentityErrors(deleteResult.Errors));
        }

        return NoContent();
    }

    private static bool IsAllowedRole(string role)
    {
        return AllowedRoles.Contains(role, StringComparer.Ordinal);
    }

    private static string FormatIdentityErrors(IEnumerable<IdentityError> errors)
    {
        return string.Join("; ", errors.Select(e => e.Description));
    }
}
