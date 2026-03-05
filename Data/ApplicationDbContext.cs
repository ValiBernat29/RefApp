using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RefApp.Models;

namespace RefApp.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Match> Matches { get; set; } = default!;

    public DbSet<MatchAssignment> MatchAssignments { get; set; } = default!;

    public DbSet<Unavailability> Unavailabilities { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Match>()
            .HasMany(m => m.Assignments)
            .WithOne(a => a.Match)
            .HasForeignKey(a => a.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ApplicationUser>()
            .HasMany(u => u.MatchAssignments)
            .WithOne(a => a.Referee)
            .HasForeignKey(a => a.RefereeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ApplicationUser>()
            .HasMany(u => u.Unavailabilities)
            .WithOne(uav => uav.Referee)
            .HasForeignKey(uav => uav.RefereeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<MatchAssignment>()
            .Property(a => a.RoleType)
            .HasConversion<string>()
            .HasMaxLength(20);
    }
}

