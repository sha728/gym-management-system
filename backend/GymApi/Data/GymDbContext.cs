using GymApi.Models;
using Microsoft.EntityFrameworkCore;

namespace GymApi.Data;

public class GymDbContext : DbContext
{
    public GymDbContext(DbContextOptions<GymDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<MembershipPlan> MembershipPlans { get; set; }
    public DbSet<Membership> Memberships { get; set; }
    public DbSet<Trainer> Trainers { get; set; }
    public DbSet<TrainerAvailability> TrainerAvailabilities { get; set; }
    public DbSet<FitnessProgram> FitnessPrograms { get; set; }
    public DbSet<Session> Sessions { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Email addresses must be unique because they are used to identify users.
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Membership>()
            .HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Prevent accidental deletion of membership records when related data is removed.
        modelBuilder.Entity<Membership>()
            .HasOne(m => m.MembershipPlan)
            .WithMany(p => p.Memberships)
            .HasForeignKey(m => m.MembershipPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TrainerAvailability>()
            .HasOne(a => a.Trainer)
            .WithMany(t => t.Availabilities)
            .HasForeignKey(a => a.TrainerId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Session>()
            .HasOne(s => s.FitnessProgram)
            .WithMany(p => p.Sessions)
            .HasForeignKey(s => s.FitnessProgramId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Session>()
            .HasOne(s => s.Trainer)
            .WithMany()
            .HasForeignKey(s => s.TrainerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Booking>()
            .HasOne(b => b.User)
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Booking>()
            .HasOne(b => b.Session)
            .WithMany(s => s.Bookings)
            .HasForeignKey(b => b.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        // A member can only book the same session once.
        modelBuilder.Entity<Booking>()
            .HasIndex(b => new { b.UserId, b.SessionId })
            .IsUnique();
    }
}