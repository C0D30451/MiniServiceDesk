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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Ticket>()
            .HasIndex(t => t.CreatedAt);

        modelBuilder.Entity<Ticket>()
            .HasIndex(t => t.CreatedByUserId);

        modelBuilder.Entity<Ticket>()
            .HasIndex(t => t.AssignedToUserId);

        modelBuilder.Entity<TicketComment>()
            .HasOne(c => c.Ticket)
            .WithMany(t => t.Comments)
            .HasForeignKey(c => c.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

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
