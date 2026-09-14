using AnyDeskMonitor.Application.Interfaces;
using AnyDeskMonitor.Domain.Entities;
using AnyDeskMonitor.Domain.Enums;
using AnyDeskMonitor.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AnyDeskMonitor.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IApplicationDbContext _db;

    public DashboardService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardStatsDto> GetStatsAsync()
    {
        var total = await _db.Computers.CountAsync();
        var online = await _db.Computers.CountAsync(c => c.Status == ComputerStatus.ONLINE);
        var offline = await _db.Computers.CountAsync(c => c.Status == ComputerStatus.OFFLINE);
        var unknown = await _db.Computers.CountAsync(c => c.Status == ComputerStatus.UNKNOWN);

        var activeAgents = await _db.Agents.CountAsync(a => a.IsEnabled && a.Status != AgentStatus.INACTIVE);
        var inactiveAgents = await _db.Agents.CountAsync(a => !a.IsEnabled || a.Status == AgentStatus.INACTIVE);

        var localAnyDeskIds = await _db.Computers.CountAsync(c => !string.IsNullOrEmpty(c.AnyDeskId));
        var remoteAnyDeskIds = await _db.RemoteAnyDeskIds.CountAsync(r => r.IsActive);

        var activeSessions = await _db.RemoteSessions.CountAsync(s => s.Status == SessionStatus.ACTIVE);
        var today = DateTime.UtcNow.Date;
        var sessionsToday = await _db.RemoteSessions.CountAsync(s => s.StartedAt >= today);

        return new DashboardStatsDto
        {
            TotalComputers = total,
            OnlineComputers = online,
            OfflineComputers = offline,
            UnknownComputers = unknown,
            ActiveAgents = activeAgents,
            InactiveAgents = inactiveAgents,
            LocalAnyDeskIds = localAnyDeskIds,
            RemoteAnyDeskIds = remoteAnyDeskIds,
            ActiveSessions = activeSessions,
            SessionsToday = sessionsToday
        };
    }
}

public class EventService : IEventService
{
    private readonly IApplicationDbContext _db;

    public EventService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<EventDto>> GetEventsAsync(string? type = null, Guid? computerId = null, int limit = 100)
    {
        var query = _db.Events.AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(e => e.Type.ToLower() == type.Trim().ToLower());
        }

        if (computerId.HasValue)
        {
            query = query.Where(e => e.ComputerId == computerId.Value);
        }

        var list = await query.OrderByDescending(e => e.CreatedAt).Take(limit).ToListAsync();
        return list.Select(e => new EventDto
        {
            Id = e.Id,
            ComputerId = e.ComputerId,
            AgentId = e.AgentId,
            Type = e.Type,
            Message = e.Message,
            User = e.User,
            CreatedAt = e.CreatedAt
        });
    }

    public async Task CreateEventAsync(Guid? computerId, Guid? agentId, string type, string message, string? user = null)
    {
        var evt = new Event
        {
            Id = Guid.NewGuid(),
            ComputerId = computerId,
            AgentId = agentId,
            Type = type,
            Message = message,
            User = user,
            CreatedAt = DateTime.UtcNow
        };
        _db.Events.Add(evt);
        await _db.SaveChangesAsync();
    }
}

public class RemoteIdService : IRemoteIdService
{
    private readonly IApplicationDbContext _db;
    private readonly IEventService _eventService;

    public RemoteIdService(IApplicationDbContext db, IEventService eventService)
    {
        _db = db;
        _eventService = eventService;
    }

    public async Task<IEnumerable<RemoteAnyDeskIdDto>> GetAllRemoteIdsAsync()
    {
        var list = await _db.RemoteAnyDeskIds.OrderByDescending(r => r.CreatedAt).ToListAsync();
        return list.Select(r =>
        {
            var geo = AnyDeskMonitor.Domain.Services.GeoLocationService.ResolveLocation(r.AnyDeskId, r.Location);
            return new RemoteAnyDeskIdDto
            {
                Id = r.Id,
                ComputerId = r.ComputerId,
                AnyDeskId = r.AnyDeskId,
                Alias = r.Alias,
                Description = r.Description,
                Location = r.Location ?? geo.Location,
                CountryCode = r.CountryCode ?? geo.CountryCode,
                CountryFlag = r.CountryFlag ?? geo.CountryFlag,
                FirstSeen = r.FirstSeen,
                LastSeen = r.LastSeen,
                IsActive = r.IsActive,
                IsAuthorized = r.IsAuthorized,
                Notes = r.Notes
            };
        });
    }

    public async Task<RemoteAnyDeskIdDto> CreateRemoteIdAsync(CreateRemoteAnyDeskIdDto dto, string? user = null)
    {
        var geo = AnyDeskMonitor.Domain.Services.GeoLocationService.ResolveLocation(dto.AnyDeskId, dto.Location);

        var entity = new RemoteAnyDeskId
        {
            Id = Guid.NewGuid(),
            ComputerId = dto.ComputerId,
            AnyDeskId = dto.AnyDeskId.Trim(),
            Alias = dto.Alias,
            Description = dto.Description,
            Location = string.IsNullOrWhiteSpace(dto.Location) ? geo.Location : dto.Location,
            CountryCode = string.IsNullOrWhiteSpace(dto.CountryCode) ? geo.CountryCode : dto.CountryCode,
            CountryFlag = string.IsNullOrWhiteSpace(dto.CountryFlag) ? geo.CountryFlag : dto.CountryFlag,
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow,
            IsActive = true,
            IsAuthorized = true,
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.RemoteAnyDeskIds.Add(entity);
        await _db.SaveChangesAsync();

        await AuditRemoteIdChangeAsync(entity.Id, entity.ComputerId, "Created", null, entity.AnyDeskId, user, "Registration of new remote AnyDesk ID");
        await _eventService.CreateEventAsync(entity.ComputerId, null, "RemoteIdAdded", $"ID Remoto AnyDesk {entity.AnyDeskId} adicionado ao computador.", user);

        return new RemoteAnyDeskIdDto
        {
            Id = entity.Id,
            ComputerId = entity.ComputerId,
            AnyDeskId = entity.AnyDeskId,
            Alias = entity.Alias,
            Description = entity.Description,
            Location = entity.Location,
            CountryCode = entity.CountryCode,
            CountryFlag = entity.CountryFlag,
            FirstSeen = entity.FirstSeen,
            LastSeen = entity.LastSeen,
            IsActive = entity.IsActive,
            IsAuthorized = entity.IsAuthorized,
            Notes = entity.Notes
        };
    }

    public async Task<RemoteAnyDeskIdDto?> UpdateRemoteIdAsync(Guid id, RemoteAnyDeskIdDto dto, string? user = null)
    {
        var entity = await _db.RemoteAnyDeskIds.FirstOrDefaultAsync(r => r.Id == id);
        if (entity == null) return null;

        var prevValue = $"{entity.AnyDeskId} (Alias: {entity.Alias}, Active: {entity.IsActive})";

        var geo = AnyDeskMonitor.Domain.Services.GeoLocationService.ResolveLocation(entity.AnyDeskId, dto.Location);

        entity.Alias = dto.Alias;
        entity.Description = dto.Description;
        entity.Location = string.IsNullOrWhiteSpace(dto.Location) ? geo.Location : dto.Location;
        entity.CountryCode = string.IsNullOrWhiteSpace(dto.CountryCode) ? geo.CountryCode : dto.CountryCode;
        entity.CountryFlag = string.IsNullOrWhiteSpace(dto.CountryFlag) ? geo.CountryFlag : dto.CountryFlag;
        entity.IsActive = dto.IsActive;
        entity.IsAuthorized = dto.IsAuthorized;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var newValue = $"{entity.AnyDeskId} (Alias: {entity.Alias}, Active: {entity.IsActive})";
        await AuditRemoteIdChangeAsync(entity.Id, entity.ComputerId, "Updated", prevValue, newValue, user, "Update remote ID settings");

        dto.Location = entity.Location;
        dto.CountryCode = entity.CountryCode;
        dto.CountryFlag = entity.CountryFlag;

        return dto;
    }

    public async Task<bool> DeleteRemoteIdAsync(Guid id, string? user = null)
    {
        var entity = await _db.RemoteAnyDeskIds.FirstOrDefaultAsync(r => r.Id == id);
        if (entity == null) return false;

        _db.RemoteAnyDeskIds.Remove(entity);
        await _db.SaveChangesAsync();

        await AuditRemoteIdChangeAsync(entity.Id, entity.ComputerId, "Removed", entity.AnyDeskId, null, user, "Deletion of remote AnyDesk ID");
        return true;
    }

    public async Task<IEnumerable<AuditDto>> GetHistoryAsync(Guid? remoteId = null)
    {
        var query = _db.RemoteAnyDeskIdHistories.AsQueryable();
        if (remoteId.HasValue)
        {
            query = query.Where(h => h.RemoteAnyDeskIdId == remoteId.Value);
        }

        var list = await query.OrderByDescending(h => h.ChangedAt).ToListAsync();
        return list.Select(h => new AuditDto
        {
            Id = h.Id,
            User = h.ChangedBy ?? "System",
            Action = h.Action,
            Entity = "RemoteAnyDeskId",
            EntityId = h.RemoteAnyDeskIdId.ToString(),
            Timestamp = h.ChangedAt,
            Details = $"Prev: {h.PreviousValue} | New: {h.NewValue} | Reason: {h.Reason}"
        });
    }

    private async Task AuditRemoteIdChangeAsync(Guid remoteId, Guid computerId, string action, string? prevValue, string? newValue, string? user, string? reason)
    {
        var history = new RemoteAnyDeskIdHistory
        {
            Id = Guid.NewGuid(),
            RemoteAnyDeskIdId = remoteId,
            ComputerId = computerId,
            Action = action,
            PreviousValue = prevValue,
            NewValue = newValue,
            ChangedBy = user ?? "System",
            ChangedAt = DateTime.UtcNow,
            Reason = reason
        };

        _db.RemoteAnyDeskIdHistories.Add(history);
        await _db.SaveChangesAsync();
    }
}

public class SessionService : ISessionService
{
    private readonly IApplicationDbContext _db;
    private readonly IEventService _eventService;

    public SessionService(IApplicationDbContext db, IEventService eventService)
    {
        _db = db;
        _eventService = eventService;
    }

    public async Task<IEnumerable<RemoteSessionDto>> GetAllSessionsAsync()
    {
        var list = await _db.RemoteSessions.OrderByDescending(s => s.StartedAt).ToListAsync();
        return list.Select(s =>
        {
            var geo = AnyDeskMonitor.Domain.Services.GeoLocationService.ResolveLocation(s.RemoteAnyDeskId, s.Location);
            return new RemoteSessionDto
            {
                Id = s.Id,
                ComputerId = s.ComputerId,
                AgentId = s.AgentId,
                LocalAnyDeskId = s.LocalAnyDeskId,
                RemoteAnyDeskId = s.RemoteAnyDeskId,
                Location = s.Location ?? geo.Location,
                CountryCode = s.CountryCode ?? geo.CountryCode,
                CountryFlag = s.CountryFlag ?? geo.CountryFlag,
                StartedAt = s.StartedAt,
                EndedAt = s.EndedAt,
                Duration = s.Duration?.ToString(@"hh\:mm\:ss"),
                Status = s.Status.ToString(),
                InitiatedBy = s.InitiatedBy
            };
        });
    }

    public async Task<RemoteSessionDto?> GetSessionByIdAsync(Guid id)
    {
        var s = await _db.RemoteSessions.FirstOrDefaultAsync(x => x.Id == id);
        if (s == null) return null;
        var geo = AnyDeskMonitor.Domain.Services.GeoLocationService.ResolveLocation(s.RemoteAnyDeskId, s.Location);
        return new RemoteSessionDto
        {
            Id = s.Id,
            ComputerId = s.ComputerId,
            AgentId = s.AgentId,
            LocalAnyDeskId = s.LocalAnyDeskId,
            RemoteAnyDeskId = s.RemoteAnyDeskId,
            Location = s.Location ?? geo.Location,
            CountryCode = s.CountryCode ?? geo.CountryCode,
            CountryFlag = s.CountryFlag ?? geo.CountryFlag,
            StartedAt = s.StartedAt,
            EndedAt = s.EndedAt,
            Duration = s.Duration?.ToString(@"hh\:mm\:ss"),
            Status = s.Status.ToString(),
            InitiatedBy = s.InitiatedBy
        };
    }

    public async Task RecordSessionAsync(Guid computerId, Guid? agentId, string remoteAnyDeskId, string? localAnyDeskId, string? initiatedBy = null)
    {
        var geo = AnyDeskMonitor.Domain.Services.GeoLocationService.ResolveLocation(remoteAnyDeskId);

        var session = new RemoteSession
        {
            Id = Guid.NewGuid(),
            ComputerId = computerId,
            AgentId = agentId,
            LocalAnyDeskId = localAnyDeskId,
            RemoteAnyDeskId = remoteAnyDeskId,
            Location = geo.Location,
            CountryCode = geo.CountryCode,
            CountryFlag = geo.CountryFlag,
            StartedAt = DateTime.UtcNow,
            Status = SessionStatus.ACTIVE,
            InitiatedBy = initiatedBy ?? "User",
            CreatedAt = DateTime.UtcNow
        };

        _db.RemoteSessions.Add(session);
        await _db.SaveChangesAsync();

        await _eventService.CreateEventAsync(computerId, agentId, "SessionRecorded", $"Sessão registada com ID AnyDesk Remoto {remoteAnyDeskId}.", initiatedBy);
    }

    public async Task<bool> KillSessionAsync(Guid id, string? user = null)
    {
        var session = await _db.RemoteSessions.FirstOrDefaultAsync(x => x.Id == id);
        if (session != null)
        {
            session.Status = SessionStatus.TERMINATED;
            session.EndedAt = DateTime.UtcNow;
            if (session.StartedAt != default)
            {
                session.Duration = session.EndedAt.Value - session.StartedAt;
            }
            await _db.SaveChangesAsync();

            await _eventService.CreateEventAsync(session.ComputerId, session.AgentId, "TaskkillSession", $"Sessão AnyDesk ({session.RemoteAnyDeskId}) encerrada via Taskkill.", user ?? "Admin");
        }

        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "taskkill.exe",
                    Arguments = "/F /T /IM AnyDesk.exe",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                p?.WaitForExit(2000);
            }
            catch { }
        }

        return true;
    }
}

public class AuditService : IAuditService
{
    private readonly IApplicationDbContext _db;

    public AuditService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<AuditDto>> GetAuditLogsAsync(int limit = 100)
    {
        var events = await _db.Events.OrderByDescending(e => e.CreatedAt).Take(limit).ToListAsync();
        return events.Select(e => new AuditDto
        {
            Id = e.Id,
            User = e.User ?? "System",
            Action = e.Type,
            Entity = "Computer/Agent",
            EntityId = e.ComputerId?.ToString(),
            Timestamp = e.CreatedAt,
            Details = e.Message
        });
    }

    public async Task LogAuditAsync(string user, string action, string entity, string? entityId = null, string? ipAddress = null, string? details = null)
    {
        var evt = new Event
        {
            Id = Guid.NewGuid(),
            Type = action,
            Message = $"[{entity}] {details} (IP: {ipAddress})",
            User = user,
            CreatedAt = DateTime.UtcNow
        };
        _db.Events.Add(evt);
        await _db.SaveChangesAsync();
    }
}
