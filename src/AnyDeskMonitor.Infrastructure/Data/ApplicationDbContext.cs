using AnyDeskMonitor.Application.Interfaces;
using AnyDeskMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AnyDeskMonitor.Infrastructure.Data;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Computer> Computers => Set<Computer>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<RemoteAnyDeskId> RemoteAnyDeskIds => Set<RemoteAnyDeskId>();
    public DbSet<RemoteAnyDeskIdHistory> RemoteAnyDeskIdHistories => Set<RemoteAnyDeskIdHistory>();
    public DbSet<RemoteSession> RemoteSessions => Set<RemoteSession>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Computer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.MachineName).IsRequired().HasMaxLength(150);
            entity.Property(e => e.OperatingSystem).HasMaxLength(100);
            entity.Property(e => e.AnyDeskId).HasMaxLength(50);
            entity.HasIndex(e => e.MachineName);
            entity.HasIndex(e => e.AnyDeskId);

            entity.HasOne(e => e.Agent)
                  .WithOne(a => a.Computer)
                  .HasForeignKey<Agent>(a => a.ComputerId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Agent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InstallationId).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.InstallationId).IsUnique();
        });

        modelBuilder.Entity<RemoteAnyDeskId>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AnyDeskId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Location).HasMaxLength(150);
            entity.Property(e => e.CountryCode).HasMaxLength(10);
            entity.Property(e => e.CountryFlag).HasMaxLength(20);
            entity.HasOne(e => e.Computer)
                  .WithMany(c => c.RemoteAnyDeskIds)
                  .HasForeignKey(e => e.ComputerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RemoteAnyDeskIdHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
        });

        modelBuilder.Entity<RemoteSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RemoteAnyDeskId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Location).HasMaxLength(150);
            entity.Property(e => e.CountryCode).HasMaxLength(10);
            entity.Property(e => e.CountryFlag).HasMaxLength(20);
            entity.HasOne(e => e.Computer)
                  .WithMany(c => c.RemoteSessions)
                  .HasForeignKey(e => e.ComputerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(500);
            entity.HasOne(e => e.Computer)
                  .WithMany(c => c.Events)
                  .HasForeignKey(e => e.ComputerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Username).IsUnique();
        });
    }
}
