using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MiniServiceDesk.Api.models;

namespace MiniServiceDesk.Api.Data;

public class AppDbContext : IdentityDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<TicketColumn> TicketColumns => Set<TicketColumn>();
    public DbSet<TicketEvent> TicketEvents => Set<TicketEvent>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Ticket>()
            .HasIndex(t => t.CreatedAt);

        modelBuilder.Entity<Ticket>()
            .HasIndex(t => t.CreatedByUserId);

        modelBuilder.Entity<Ticket>()
            .HasIndex(t => t.AssignedToUserId);

        modelBuilder.Entity<Ticket>()
            .HasIndex(t => t.DueAt);

        modelBuilder.Entity<TicketComment>()
            .HasOne(c => c.Ticket)
            .WithMany(t => t.Comments)
            .HasForeignKey(c => c.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TicketEvent>()
            .HasOne(e => e.Ticket)
            .WithMany(t => t.Events)
            .HasForeignKey(e => e.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TicketEvent>()
            .HasIndex(e => new { e.TicketId, e.CreatedAt });

        modelBuilder.Entity<TicketAttachment>()
            .HasOne(a => a.Ticket)
            .WithMany(t => t.Attachments)
            .HasForeignKey(a => a.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TicketAttachment>()
            .HasIndex(a => new { a.TicketId, a.CreatedAt });

        modelBuilder.Entity<UserNotification>()
            .HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });

        modelBuilder.Entity<TicketColumn>()
            .HasIndex(c => new { c.OwnerUserId, c.Name })
            .IsUnique();

        modelBuilder.Entity<Ticket>()
            .HasOne(t => t.TicketColumn)
            .WithMany()
            .HasForeignKey(t => t.TicketColumnId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
