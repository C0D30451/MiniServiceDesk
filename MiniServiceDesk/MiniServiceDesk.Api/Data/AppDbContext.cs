// MiniServiceDesk.Api/Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using MiniServiceDesk.Api.models;
using System;                           // per DateTime
using System.ComponentModel.DataAnnotations;  // per [Required], [MaxLength]

namespace MiniServiceDesk.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Ticket>()
            .HasIndex(t => t.CreatedAt);
    }
}
