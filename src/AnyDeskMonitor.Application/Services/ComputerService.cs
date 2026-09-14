using AnyDeskMonitor.Application.Interfaces;
using AnyDeskMonitor.Domain.Entities;
using AnyDeskMonitor.Domain.Enums;
using AnyDeskMonitor.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AnyDeskMonitor.Application.Services;

public class ComputerService : IComputerService
{
    private readonly IApplicationDbContext _db;
    private readonly IEventService _eventService;

    public ComputerService(IApplicationDbContext db, IEventService eventService)
    {
        _db = db;
        _eventService = eventService;
    }

    public async Task<IEnumerable<ComputerDto>> GetAllComputersAsync(string? search = null, string? status = null, string? os = null)
    {
        var query = _db.Computers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(searchLower) ||
                                     c.MachineName.ToLower().Contains(searchLower) ||
                                     (c.AnyDeskId != null && c.AnyDeskId.ToLower().Contains(searchLower)));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ComputerStatus>(status, true, out var statusEnum))
        {
            query = query.Where(c => c.Status == statusEnum);
        }

        if (!string.IsNullOrWhiteSpace(os))
        {
            var osLower = os.Trim().ToLower();
            query = query.Where(c => c.OperatingSystem.ToLower().Contains(osLower));
        }

        var list = await query.OrderByDescending(c => c.LastSeen).ToListAsync();
        return list.Select(ToDto);
    }

    public async Task<ComputerDto?> GetComputerByIdAsync(Guid id)
    {
        var computer = await _db.Computers.FirstOrDefaultAsync(c => c.Id == id);
        return computer != null ? ToDto(computer) : null;
    }

    public async Task<ComputerDto?> UpdateComputerAsync(Guid id, ComputerDto dto)
    {
        var computer = await _db.Computers.FirstOrDefaultAsync(c => c.Id == id);
        if (computer == null) return null;

        computer.Name = string.IsNullOrWhiteSpace(dto.Name) ? computer.Name : dto.Name;
        computer.AnyDeskId = dto.AnyDeskId;
        computer.IsEnabled = dto.IsEnabled;
        computer.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _eventService.CreateEventAsync(computer.Id, computer.AgentId, "ComputerUpdated", $"Computador {computer.MachineName} atualizado.");

        return ToDto(computer);
    }

    public async Task<bool> DeleteComputerAsync(Guid id)
    {
        var computer = await _db.Computers.FirstOrDefaultAsync(c => c.Id == id);
        if (computer == null) return false;

        _db.Computers.Remove(computer);
        await _db.SaveChangesAsync();
        await _eventService.CreateEventAsync(null, null, "ComputerDeleted", $"Computador {computer.MachineName} desativado/removido.");
        return true;
    }

    public async Task<bool> SyncComputerAsync(Guid id)
    {
        var computer = await _db.Computers.FirstOrDefaultAsync(c => c.Id == id);
        if (computer == null) return false;

        var now = DateTime.UtcNow;
        computer.Status = ComputerStatus.ONLINE;
        computer.LastHeartbeat = now;
        computer.LastSeen = now;
        computer.UpdatedAt = now;

        var agent = await _db.Agents.FirstOrDefaultAsync(a => a.ComputerId == computer.Id || a.MachineName.ToLower() == computer.MachineName.ToLower());
        if (agent != null)
        {
            agent.Status = AgentStatus.ACTIVE;
            agent.IsEnabled = true;
            agent.LastHeartbeat = now;
            agent.LastSeen = now;
            agent.UpdatedAt = now;
        }

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

                if (anydeskInfo.RemoteIds != null && anydeskInfo.RemoteIds.Any())
                {
                    foreach (var r in anydeskInfo.RemoteIds)
                    {
                        if (string.IsNullOrWhiteSpace(r.AnyDeskId)) continue;
                        if (r.AnyDeskId == "123456789" || r.AnyDeskId == "456789123") continue;

                        var existing = await _db.RemoteAnyDeskIds.FirstOrDefaultAsync(x => x.ComputerId == computer.Id && x.AnyDeskId == r.AnyDeskId);
                        var geo = AnyDeskMonitor.Domain.Services.GeoLocationService.ResolveLocation(r.AnyDeskId);
                        if (existing == null)
                        {
                            _db.RemoteAnyDeskIds.Add(new RemoteAnyDeskId
                            {
                                Id = Guid.NewGuid(),
                                ComputerId = computer.Id,
                                AnyDeskId = r.AnyDeskId,
                                Alias = r.Alias ?? "Detetado no Sistema",
                                Description = "ID AnyDesk detetado automaticamente",
                                Location = geo.Location,
                                CountryCode = geo.CountryCode,
                                CountryFlag = geo.CountryFlag,
                                FirstSeen = DateTime.UtcNow,
                                LastSeen = DateTime.UtcNow,
                                IsActive = true,
                                IsAuthorized = true
                            });
                        }
                        else
                        {
                            existing.LastSeen = DateTime.UtcNow;
                            existing.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                }

                if (anydeskInfo.Sessions != null && anydeskInfo.Sessions.Any())
                {
                    foreach (var s in anydeskInfo.Sessions)
                    {
                        if (string.IsNullOrWhiteSpace(s.RemoteAnyDeskId)) continue;
                        var dir = s.Direction ?? "ENTRADA";
                        var existingSession = await _db.RemoteSessions.FirstOrDefaultAsync(x => x.ComputerId == computer.Id && x.RemoteAnyDeskId == s.RemoteAnyDeskId && x.InitiatedBy == dir);
                        var geo = AnyDeskMonitor.Domain.Services.GeoLocationService.ResolveLocation(s.RemoteAnyDeskId);
                        if (existingSession == null)
                        {
                            _db.RemoteSessions.Add(new RemoteSession
                            {
                                Id = Guid.NewGuid(),
                                ComputerId = computer.Id,
                                AgentId = agent?.Id,
                                LocalAnyDeskId = s.LocalAnyDeskId ?? computer.AnyDeskId,
                                RemoteAnyDeskId = s.RemoteAnyDeskId,
                                Location = geo.Location,
                                CountryCode = geo.CountryCode,
                                CountryFlag = geo.CountryFlag,
                                StartedAt = s.StartedAt,
                                EndedAt = s.EndedAt,
                                Status = SessionStatus.ACTIVE,
                                InitiatedBy = dir
                            });
                        }
                    }
                }
            }
        }
        catch { }

        await _db.SaveChangesAsync();
        await _eventService.CreateEventAsync(computer.Id, agent?.Id, "ComputerSynced", $"Sincronização efetuada com sucesso para {computer.MachineName}.");
        return true;
    }

    public async Task<IEnumerable<RemoteAnyDeskIdDto>> GetRemoteIdsByComputerIdAsync(Guid computerId)
    {
        var list = await _db.RemoteAnyDeskIds.Where(r => r.ComputerId == computerId).ToListAsync();
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

    public async Task<IEnumerable<EventDto>> GetEventsByComputerIdAsync(Guid computerId)
    {
        var list = await _db.Events.Where(e => e.ComputerId == computerId).OrderByDescending(e => e.CreatedAt).Take(50).ToListAsync();
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

    private static ComputerDto ToDto(Computer c) => new()
    {
        Id = c.Id,
        Name = string.IsNullOrEmpty(c.Name) ? c.MachineName : c.Name,
        MachineName = c.MachineName,
        OperatingSystem = c.OperatingSystem,
        OperatingSystemVersion = c.OperatingSystemVersion,
        IPAddress = c.IPAddress,
        MacAddress = c.MacAddress,
        AnyDeskId = c.AnyDeskId,
        Status = c.Status.ToString(),
        AgentId = c.AgentId,
        LastHeartbeat = c.LastHeartbeat,
        FirstSeen = c.FirstSeen,
        LastSeen = c.LastSeen,
        IsEnabled = c.IsEnabled
    };
}
