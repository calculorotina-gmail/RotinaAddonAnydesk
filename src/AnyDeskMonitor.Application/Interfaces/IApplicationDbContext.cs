using AnyDeskMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AnyDeskMonitor.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Computer> Computers { get; }
    DbSet<Agent> Agents { get; }
    DbSet<RemoteAnyDeskId> RemoteAnyDeskIds { get; }
    DbSet<RemoteAnyDeskIdHistory> RemoteAnyDeskIdHistories { get; }
    DbSet<RemoteSession> RemoteSessions { get; }
    DbSet<Event> Events { get; }
    DbSet<User> Users { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAt) GenerateToken(User user);
}

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string storedHash);
}
