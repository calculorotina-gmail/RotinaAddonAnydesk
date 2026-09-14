using AnyDeskMonitor.Application.Interfaces;
using AnyDeskMonitor.Domain.Entities;
using AnyDeskMonitor.Domain.Enums;
using AnyDeskMonitor.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AnyDeskMonitor.Application.Services;

public class AgentService : IAgentService
{
    private readonly IApplicationDbContext _db;
    private readonly IEventService _eventService;

    public AgentService(IApplicationDbContext db, IEventService eventService)
    {
        _db = db;
        _eventService = eventService;
    }

    public async Task<AgentStatusDto> RegisterAgentAsync(AgentRegistrationDto dto)
    {
        var agent = await _db.Agents.FirstOrDefaultAsync(a => a.InstallationId == dto.InstallationId);
        var computer = await _db.Computers.FirstOrDefaultAsync(c => c.MachineName == dto.MachineName);

        if (computer == null)
        {
            computer = new Computer
            {
                Id = Guid.NewGuid(),
                Name = dto.MachineName,
                MachineName = dto.MachineName,
                OperatingSystem = dto.OperatingSystem,
                OperatingSystemVersion = dto.OperatingSystemVersion,
                IPAddress = dto.IPAddress,
                AnyDeskId = dto.AnyDeskId,
                Status = ComputerStatus.ONLINE,
                FirstSeen = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow,
                LastHeartbeat = DateTime.UtcNow
            };
            _db.Computers.Add(computer);
        }
        else
        {
            computer.OperatingSystem = dto.OperatingSystem;
            computer.OperatingSystemVersion = dto.OperatingSystemVersion;
            computer.IPAddress = dto.IPAddress;
            if (!string.IsNullOrWhiteSpace(dto.AnyDeskId))
            {
                computer.AnyDeskId = dto.AnyDeskId;
            }
            computer.Status = ComputerStatus.ONLINE;
            computer.LastSeen = DateTime.UtcNow;
            computer.LastHeartbeat = DateTime.UtcNow;
            computer.UpdatedAt = DateTime.UtcNow;
        }

        if (agent == null)
        {
            agent = await _db.Agents.FirstOrDefaultAsync(a => a.ComputerId == computer.Id || a.MachineName == dto.MachineName);
        }

        if (agent == null)
        {
            agent = new Agent
            {
                Id = Guid.NewGuid(),
                InstallationId = dto.InstallationId,
                ComputerId = computer.Id,
                MachineName = dto.MachineName,
                OperatingSystem = dto.OperatingSystem,
                IPAddress = dto.IPAddress,
                Version = dto.Version,
                Status = AgentStatus.ACTIVE,
                IsEnabled = true,
                RegisteredAt = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow,
                LastHeartbeat = DateTime.UtcNow
            };
            _db.Agents.Add(agent);
        }
        else
        {
            agent.InstallationId = dto.InstallationId;
            agent.ComputerId = computer.Id;
            agent.MachineName = dto.MachineName;
            agent.OperatingSystem = dto.OperatingSystem;
            agent.IPAddress = dto.IPAddress;
            agent.Version = dto.Version;
            agent.Status = AgentStatus.ACTIVE;
            agent.IsEnabled = true;
            agent.LastSeen = DateTime.UtcNow;
            agent.LastHeartbeat = DateTime.UtcNow;
            agent.UpdatedAt = DateTime.UtcNow;
        }

        computer.AgentId = agent.Id;

        await SyncRemoteDataAsync(computer, agent, dto.RemoteIds, dto.RemoteSessions);

        await _db.SaveChangesAsync();
        await _eventService.CreateEventAsync(computer.Id, agent.Id, "AgentRegistered", $"Agente {dto.InstallationId} registado em {dto.MachineName}.");

        return ToDto(agent);
    }

    public async Task<AgentStatusDto?> ProcessHeartbeatAsync(HeartbeatDto dto)
    {
        var agent = await _db.Agents.FirstOrDefaultAsync(a => a.InstallationId == dto.InstallationId);
        if (agent == null)
        {
            var computer = await _db.Computers.FirstOrDefaultAsync(c => c.MachineName == dto.MachineName);
            if (computer != null)
            {
                agent = await _db.Agents.FirstOrDefaultAsync(a => a.ComputerId == computer.Id || a.MachineName == dto.MachineName);
            }
        }

        if (agent == null)
        {
            return await RegisterAgentAsync(new AgentRegistrationDto
            {
                InstallationId = dto.InstallationId,
                MachineName = dto.MachineName,
                OperatingSystem = dto.OperatingSystem,
                IPAddress = dto.IPAddress,
                Version = dto.Version,
                AnyDeskId = dto.AnyDeskId,
                RemoteIds = dto.RemoteIds,
                RemoteSessions = dto.RemoteSessions
            });
        }

        var now = DateTime.UtcNow;
        agent.InstallationId = dto.InstallationId;
        agent.LastHeartbeat = now;
        agent.LastSeen = now;
        agent.IPAddress = dto.IPAddress;
        agent.Version = dto.Version;
        agent.Status = AgentStatus.ACTIVE;
        agent.IsEnabled = true;
        agent.UpdatedAt = now;

        if (agent.ComputerId.HasValue)
        {
            var computer = await _db.Computers.FirstOrDefaultAsync(c => c.Id == agent.ComputerId.Value);
            if (computer != null)
            {
                computer.LastHeartbeat = now;
                computer.LastSeen = now;
                computer.IPAddress = dto.IPAddress;
                computer.Status = ComputerStatus.ONLINE;
                if (!string.IsNullOrWhiteSpace(dto.AnyDeskId))
                {
                    computer.AnyDeskId = dto.AnyDeskId;
                }
                computer.UpdatedAt = now;

                await SyncRemoteDataAsync(computer, agent, dto.RemoteIds, dto.RemoteSessions);
            }
        }

        await _db.SaveChangesAsync();
        return ToDto(agent);
    }

    private async Task SyncRemoteDataAsync(Computer computer, Agent agent, List<CreateRemoteAnyDeskIdDto> remoteIds, List<RemoteSessionDto> remoteSessions)
    {
        var localAnyDeskId = !string.IsNullOrWhiteSpace(computer.AnyDeskId) 
            ? computer.AnyDeskId 
            : remoteSessions?.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.LocalAnyDeskId))?.LocalAnyDeskId;

        if (string.IsNullOrWhiteSpace(computer.AnyDeskId) && !string.IsNullOrWhiteSpace(localAnyDeskId))
        {
            computer.AnyDeskId = localAnyDeskId;
        }

        if (remoteIds != null && remoteIds.Any())
        {
            foreach (var r in remoteIds)
            {
                if (string.IsNullOrWhiteSpace(r.AnyDeskId)) continue;
                if (r.AnyDeskId == "123456789" || r.AnyDeskId == "456789123") continue;

                var existing = await _db.RemoteAnyDeskIds.FirstOrDefaultAsync(x => x.ComputerId == computer.Id && x.AnyDeskId == r.AnyDeskId);
                var geo = AnyDeskMonitor.Domain.Services.GeoLocationService.ResolveLocation(r.AnyDeskId, r.Location);
                if (existing == null)
                {
                    var newRemoteId = new RemoteAnyDeskId
                    {
                        Id = Guid.NewGuid(),
                        ComputerId = computer.Id,
                        AnyDeskId = r.AnyDeskId,
                        Alias = r.Alias ?? "Detetado no Sistema",
                        Description = r.Description ?? "Detetado automaticamente pelo Agente",
                        Location = r.Location ?? geo.Location,
                        CountryCode = r.CountryCode ?? geo.CountryCode,
                        CountryFlag = r.CountryFlag ?? geo.CountryFlag,
                        FirstSeen = DateTime.UtcNow,
                        LastSeen = DateTime.UtcNow,
                        IsActive = true,
                        IsAuthorized = true
                    };
                    _db.RemoteAnyDeskIds.Add(newRemoteId);
                    await _eventService.CreateEventAsync(computer.Id, agent.Id, "RemoteIdDetected", $"ID Remoto {r.AnyDeskId} detetado para o computador {computer.MachineName}.");
                }
                else
                {
                    existing.LastSeen = DateTime.UtcNow;
                    existing.UpdatedAt = DateTime.UtcNow;
                    if (string.IsNullOrEmpty(existing.Location))
                    {
                        existing.Location = geo.Location;
                        existing.CountryCode = geo.CountryCode;
                        existing.CountryFlag = geo.CountryFlag;
                    }
                }
            }
        }

        if (remoteSessions != null && remoteSessions.Any())
        {
            foreach (var s in remoteSessions)
            {
                if (string.IsNullOrWhiteSpace(s.RemoteAnyDeskId)) continue;
                if (s.LocalAnyDeskId == "000000000" || s.LocalAnyDeskId == "1738812731") continue;

                var localId = !string.IsNullOrWhiteSpace(s.LocalAnyDeskId) ? s.LocalAnyDeskId : computer.AnyDeskId;
                var geo = AnyDeskMonitor.Domain.Services.GeoLocationService.ResolveLocation(s.RemoteAnyDeskId, s.Location);

                var existingSession = await _db.RemoteSessions.FirstOrDefaultAsync(x => 
                    x.ComputerId == computer.Id && 
                    x.RemoteAnyDeskId == s.RemoteAnyDeskId && 
                    x.InitiatedBy == s.InitiatedBy);

                if (existingSession == null)
                {
                    var newSession = new RemoteSession
                    {
                        Id = Guid.NewGuid(),
                        ComputerId = computer.Id,
                        AgentId = agent.Id,
                        LocalAnyDeskId = localId,
                        RemoteAnyDeskId = s.RemoteAnyDeskId,
                        Location = s.Location ?? geo.Location,
                        CountryCode = s.CountryCode ?? geo.CountryCode,
                        CountryFlag = s.CountryFlag ?? geo.CountryFlag,
                        StartedAt = s.StartedAt,
                        EndedAt = s.EndedAt,
                        Status = Enum.TryParse<SessionStatus>(s.Status, true, out var parsedStatus) ? parsedStatus : SessionStatus.ACTIVE,
                        InitiatedBy = s.InitiatedBy
                    };
                    _db.RemoteSessions.Add(newSession);
                    await _eventService.CreateEventAsync(computer.Id, agent.Id, "RemoteSessionDetected", $"Sessão Remota ({s.InitiatedBy}): ID Remoto {s.RemoteAnyDeskId} <-> ID Local {localId}.");
                }
                else
                {
                    if (string.IsNullOrEmpty(existingSession.Location))
                    {
                        existingSession.Location = geo.Location;
                        existingSession.CountryCode = geo.CountryCode;
                        existingSession.CountryFlag = geo.CountryFlag;
                    }
                }
            }
        }
    }

    public async Task<IEnumerable<AgentStatusDto>> GetAllAgentsAsync()
    {
        var list = await _db.Agents.OrderByDescending(a => a.LastSeen).ToListAsync();
        return list.Select(ToDto);
    }

    public async Task<AgentStatusDto?> GetAgentByIdAsync(Guid id)
    {
        var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == id);
        return agent != null ? ToDto(agent) : null;
    }

    public async Task<bool> EnableAgentAsync(Guid id)
    {
        var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == id);
        if (agent == null) return false;

        agent.IsEnabled = true;
        agent.Status = AgentStatus.ACTIVE;
        agent.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _eventService.CreateEventAsync(agent.ComputerId, agent.Id, "AgentEnabled", $"Agente {agent.InstallationId} ativado.");
        return true;
    }

    public async Task<bool> DisableAgentAsync(Guid id)
    {
        var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == id);
        if (agent == null) return false;

        agent.IsEnabled = false;
        agent.Status = AgentStatus.INACTIVE;
        agent.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _eventService.CreateEventAsync(agent.ComputerId, agent.Id, "AgentDisabled", $"Agente {agent.InstallationId} desativado.");
        return true;
    }

    public async Task<bool> SyncAgentAsync(Guid id)
    {
        var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == id);
        if (agent == null) return false;

        var now = DateTime.UtcNow;
        agent.Status = AgentStatus.ACTIVE;
        agent.IsEnabled = true;
        agent.LastHeartbeat = now;
        agent.LastSeen = now;
        agent.UpdatedAt = now;

        Computer? computer = null;
        if (agent.ComputerId.HasValue)
        {
            computer = await _db.Computers.FirstOrDefaultAsync(c => c.Id == agent.ComputerId.Value);
        }
        if (computer == null)
        {
            computer = await _db.Computers.FirstOrDefaultAsync(c => c.MachineName.ToLower() == agent.MachineName.ToLower());
        }

        if (computer != null)
        {
            computer.Status = ComputerStatus.ONLINE;
            computer.LastHeartbeat = now;
            computer.LastSeen = now;
            computer.UpdatedAt = now;

            try
            {
                var provider = new AnyDeskMonitor.Domain.Services.LocalAgentAnyDeskProvider();
                var anydeskInfo = await provider.GetAnyDeskInfoAsync();
                if (anydeskInfo != null)
                {
                    if (!string.IsNullOrWhiteSpace(anydeskInfo.AnyDeskId))
                    {
                        computer.AnyDeskId = anydeskInfo.AnyDeskId;
                    }
                    var remoteIdDtos = anydeskInfo.RemoteIds?.Select(r => new CreateRemoteAnyDeskIdDto { AnyDeskId = r.AnyDeskId, Alias = r.Alias, Description = "Detetado no Sistema" }).ToList() ?? new();
                    var sessionDtos = anydeskInfo.Sessions?.Select(s => new RemoteSessionDto { LocalAnyDeskId = s.LocalAnyDeskId, RemoteAnyDeskId = s.RemoteAnyDeskId, StartedAt = s.StartedAt, EndedAt = s.EndedAt, Status = s.Status, InitiatedBy = s.Direction ?? "ENTRADA" }).ToList() ?? new();

                    await SyncRemoteDataAsync(computer, agent, remoteIdDtos, sessionDtos);
                }
            }
            catch { }
        }

        await _db.SaveChangesAsync();
        await _eventService.CreateEventAsync(agent.ComputerId, agent.Id, "AgentSyncRequested", $"Sincronização forçada efetuada para o agente {agent.InstallationId}.");
        return true;
    }

    private static AgentStatusDto ToDto(Agent a) => new()
    {
        Id = a.Id,
        InstallationId = a.InstallationId,
        ComputerId = a.ComputerId,
        MachineName = a.MachineName,
        OperatingSystem = a.OperatingSystem,
        IPAddress = a.IPAddress,
        Version = a.Version,
        Status = a.Status.ToString(),
        LastHeartbeat = a.LastHeartbeat,
        IsEnabled = a.IsEnabled
    };
}
