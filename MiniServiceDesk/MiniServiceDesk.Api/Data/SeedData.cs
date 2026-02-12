using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniServiceDesk.Api.models;

namespace MiniServiceDesk.Api.Data;

public static class SeedData
{
    public static async Task EnsureSeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        var roles = new[] { "User", "Agent", "Admin" };
        foreach (var role in roles)
        {
            if (!await roleMgr.RoleExistsAsync(role))
            {
                await roleMgr.CreateAsync(new IdentityRole(role));
            }
        }

        var demoUser = await EnsureUser(userMgr, "demo.user", "Passw0rd!", "User");
        var demoAgent = await EnsureUser(userMgr, "demo.agent", "Passw0rd!", "Agent");
        var demoAdmin = await EnsureUser(userMgr, "demo.admin", "Passw0rd!", "Admin");

        await EnsureDemoTicketsAsync(db, env, demoUser, demoAgent, demoAdmin);
    }

    private static async Task<IdentityUser> EnsureUser(UserManager<IdentityUser> userMgr, string username, string password, string role)
    {
        var user = await userMgr.FindByNameAsync(username);
        if (user is null)
        {
            user = new IdentityUser
            {
                UserName = username,
                Email = $"{username}@miniservicedesk.local"
            };
            var result = await userMgr.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
        else if (string.IsNullOrWhiteSpace(user.Email))
        {
            user.Email = $"{username}@miniservicedesk.local";
            await userMgr.UpdateAsync(user);
        }

        if (!await userMgr.IsInRoleAsync(user, role))
        {
            await userMgr.AddToRoleAsync(user, role);
        }

        return user;
    }

    private static async Task EnsureDemoTicketsAsync(
        AppDbContext db,
        IWebHostEnvironment env,
        IdentityUser demoUser,
        IdentityUser demoAgent,
        IdentityUser demoAdmin)
    {
        // Seed data intentionally small and explicit so all new features are testable immediately.
        // This method can run at every app startup because all downstream "Ensure*" helpers are idempotent:
        // each helper first checks if the target row already exists, and inserts only when missing.
        var now = DateTime.UtcNow;

        var overdueTicket = await EnsureDemoTicketAsync(
            db,
            title: "[Seed] VPN client crashes on startup",
            description: "VPN client crashes on startup for multiple users after latest patch. Need triage and fix path.",
            category: "IT",
            priority: TicketPriority.High,
            status: TicketStatus.InProgress,
            dueAt: now.AddDays(-1),
            createdBy: demoUser,
            assignedTo: demoAgent,
            createdAt: now.AddDays(-3));

        var dueSoonTicket = await EnsureDemoTicketAsync(
            db,
            title: "[Seed] Payroll export validation issue",
            description: "Payroll export fails validation when comments contain accented characters. Repro attached in logs.",
            category: "HR",
            priority: TicketPriority.Critical,
            status: TicketStatus.Waiting,
            dueAt: now.AddHours(10),
            createdBy: demoUser,
            assignedTo: null,
            createdAt: now.AddDays(-1));

        var onTrackTicket = await EnsureDemoTicketAsync(
            db,
            title: "[Seed] Shared printer setup request",
            description: "Need printer mapping for finance floor and default paper profile set to duplex.",
            category: "Facilities",
            priority: TicketPriority.Medium,
            status: TicketStatus.Open,
            dueAt: now.AddDays(7),
            createdBy: demoAdmin,
            assignedTo: demoUser,
            createdAt: now.AddHours(-8));

        await EnsureSeedCommentAsync(
            db,
            overdueTicket.Id,
            demoUser,
            "Issue started after Windows update KB-2026. Crash dump available.");
        await EnsureSeedCommentAsync(
            db,
            overdueTicket.Id,
            demoAgent,
            "Acknowledged. Reproduced on two machines, collecting event logs.");

        await EnsureSeedEventAsync(
            db,
            overdueTicket.Id,
            "seed_workflow",
            "Seed event: assignment and in-progress workflow created for demo.",
            demoAgent);
        await EnsureSeedEventAsync(
            db,
            dueSoonTicket.Id,
            "seed_workflow",
            "Seed event: critical ticket waiting for external input.",
            demoUser);
        await EnsureSeedEventAsync(
            db,
            onTrackTicket.Id,
            "seed_workflow",
            "Seed event: user-assigned ticket to validate assignee roles.",
            demoAdmin);

        await EnsureSeedNotificationAsync(
            db,
            demoAgent,
            overdueTicket.Id,
            "seed_notification",
            $"[Seed] You were assigned ticket #{overdueTicket.Id}.");
        await EnsureSeedNotificationAsync(
            db,
            demoUser,
            dueSoonTicket.Id,
            "seed_notification",
            $"[Seed] Ticket #{dueSoonTicket.Id} is due soon.");

        await EnsureSeedAttachmentAsync(db, env, overdueTicket, demoUser);
        await db.SaveChangesAsync();
    }

    private static async Task<Ticket> EnsureDemoTicketAsync(
        AppDbContext db,
        string title,
        string description,
        string category,
        TicketPriority priority,
        TicketStatus status,
        DateTime dueAt,
        IdentityUser createdBy,
        IdentityUser? assignedTo,
        DateTime createdAt)
    {
        // Important: no recursion here.
        // EnsureDemoTicketAsync does NOT call itself; it only queries/inserts one ticket and returns it.
        // So there is no call chain that can produce an infinite loop.
        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Title == title);
        if (ticket is not null)
        {
            // Existing row found -> reuse it.
            // This is the first guard that prevents duplicate tickets across restarts.
            return ticket;
        }

        var inboxMaxSort = await db.Tickets
            .Where(t => t.TicketColumnId == null)
            .Select(t => (int?)t.SortOrderInColumn)
            .MaxAsync() ?? 0;

        ticket = new Ticket
        {
            Title = title,
            Description = description,
            Category = category,
            Priority = priority,
            Status = status,
            CreatedByUserId = createdBy.Id,
            CreatedByUserName = createdBy.UserName,
            AssignedToUserId = assignedTo?.Id,
            AssignedToUserName = assignedTo?.UserName,
            AssignedAt = assignedTo is null ? null : createdAt.AddHours(2),
            DueAt = dueAt,
            SortOrderInColumn = inboxMaxSort + 10,
            CreatedAt = createdAt,
            UpdatedAt = DateTime.UtcNow
        };

        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        await EnsureSeedEventAsync(
            db,
            ticket.Id,
            "ticket_created",
            $"Seed ticket created by {createdBy.UserName}.",
            createdBy);

        return ticket;
    }

    private static async Task EnsureSeedCommentAsync(AppDbContext db, int ticketId, IdentityUser author, string text)
    {
        // Idempotency guard: same TicketId + same comment text => do not insert again.
        var existing = await db.TicketComments.AnyAsync(c => c.TicketId == ticketId && c.Text == text);
        if (existing)
        {
            return;
        }

        db.TicketComments.Add(new TicketComment
        {
            TicketId = ticketId,
            Text = text,
            AuthorUserId = author.Id,
            AuthorUserName = author.UserName ?? "seed-user",
            CreatedAt = DateTime.UtcNow
        });
    }

    private static async Task EnsureSeedEventAsync(
        AppDbContext db,
        int ticketId,
        string eventType,
        string message,
        IdentityUser actor)
    {
        // Idempotency guard for events: same TicketId + EventType + Message => skip insert.
        var existing = await db.TicketEvents.AnyAsync(e =>
            e.TicketId == ticketId &&
            e.EventType == eventType &&
            e.Message == message);
        if (existing)
        {
            return;
        }

        db.TicketEvents.Add(new TicketEvent
        {
            TicketId = ticketId,
            EventType = eventType,
            Message = message,
            ActorUserId = actor.Id,
            ActorUserName = actor.UserName,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static async Task EnsureSeedNotificationAsync(
        AppDbContext db,
        IdentityUser recipient,
        int ticketId,
        string notificationType,
        string message)
    {
        // Idempotency guard for notifications: same recipient/ticket/type/message => skip insert.
        var existing = await db.UserNotifications.AnyAsync(n =>
            n.UserId == recipient.Id &&
            n.TicketId == ticketId &&
            n.NotificationType == notificationType &&
            n.Message == message);
        if (existing)
        {
            return;
        }

        db.UserNotifications.Add(new UserNotification
        {
            UserId = recipient.Id,
            UserName = recipient.UserName ?? "seed-user",
            TicketId = ticketId,
            NotificationType = notificationType,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static async Task EnsureSeedAttachmentAsync(
        AppDbContext db,
        IWebHostEnvironment env,
        Ticket ticket,
        IdentityUser uploader)
    {
        const string seedFileName = "seed-log.txt";

        // Idempotency guard for attachments: same ticket + same original filename => skip DB insert.
        var existing = await db.TicketAttachments.AnyAsync(a =>
            a.TicketId == ticket.Id &&
            a.OriginalFileName == seedFileName);
        if (existing)
        {
            return;
        }

        var relativeDirectory = Path.Combine("ticket-attachments", ticket.Id.ToString());
        var absoluteDirectory = Path.Combine(env.ContentRootPath, relativeDirectory);
        Directory.CreateDirectory(absoluteDirectory);

        var absolutePath = Path.Combine(absoluteDirectory, seedFileName);
        if (!File.Exists(absolutePath))
        {
            var content = $"Seed attachment for ticket #{ticket.Id}{Environment.NewLine}Generated at {DateTime.UtcNow:u}";
            await File.WriteAllTextAsync(absolutePath, content);
        }

        var relativePath = Path.Combine(relativeDirectory, seedFileName).Replace("\\", "/");
        var fileInfo = new FileInfo(absolutePath);

        db.TicketAttachments.Add(new TicketAttachment
        {
            TicketId = ticket.Id,
            OriginalFileName = seedFileName,
            StoredFileName = seedFileName,
            StoredRelativePath = relativePath,
            ContentType = "text/plain",
            FileSizeBytes = fileInfo.Exists ? fileInfo.Length : 0,
            UploadedByUserId = uploader.Id,
            UploadedByUserName = uploader.UserName,
            CreatedAt = DateTime.UtcNow
        });
    }
}
