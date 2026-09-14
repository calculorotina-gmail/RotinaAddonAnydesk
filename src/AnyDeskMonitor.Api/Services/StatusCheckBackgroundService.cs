using AnyDeskMonitor.Api.Hubs;
using AnyDeskMonitor.Application.Interfaces;
using AnyDeskMonitor.Domain.Entities;
using AnyDeskMonitor.Domain.Enums;
using AnyDeskMonitor.Infrastructure.Data;
using AnyDeskMonitor.Shared.Constants;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AnyDeskMonitor.Api.Services;

public class StatusCheckBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<AgentHub> _hubContext;
    private readonly ILogger<StatusCheckBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(15);
    private readonly TimeSpan _offlineTimeout = TimeSpan.FromSeconds(120);

    public StatusCheckBackgroundService(
        IServiceProvider serviceProvider,
        IHubContext<AgentHub> hubContext,
        ILogger<StatusCheckBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("StatusCheckBackgroundService iniciado com intervalo de {Interval}s e timeout de {Timeout}s.", _checkInterval.TotalSeconds, _offlineTimeout.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckComputerStatusesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar estados dos computadores em segundo plano.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task CheckComputerStatusesAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();

        var currentMachine = Environment.MachineName;
        var currentMachineLower = currentMachine.ToLower();

        // Keep local computer and agent ONLINE / ACTIVE when running on local machine
        var localComps = await db.Computers.Where(c => c.MachineName.ToLower() == currentMachineLower).ToListAsync(stoppingToken);
        foreach (var localComp in localComps)
        {
            localComp.Status = ComputerStatus.ONLINE;
            localComp.LastHeartbeat = DateTime.UtcNow;
            localComp.LastSeen = DateTime.UtcNow;
            localComp.UpdatedAt = DateTime.UtcNow;

            if (localComp.AgentId.HasValue)
            {
                var agent = await db.Agents.FirstOrDefaultAsync(a => a.Id == localComp.AgentId.Value, stoppingToken);
                if (agent != null)
                {
                    agent.Status = AgentStatus.ACTIVE;
                    agent.IsEnabled = true;
                    agent.LastHeartbeat = DateTime.UtcNow;
                    agent.LastSeen = DateTime.UtcNow;
                    agent.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        var localAgents = await db.Agents.Where(a => a.MachineName.ToLower() == currentMachineLower).ToListAsync(stoppingToken);
        foreach (var localAgent in localAgents)
        {
            localAgent.Status = AgentStatus.ACTIVE;
            localAgent.IsEnabled = true;
            localAgent.LastHeartbeat = DateTime.UtcNow;
            localAgent.LastSeen = DateTime.UtcNow;
            localAgent.UpdatedAt = DateTime.UtcNow;
        }

        var cutoffTime = DateTime.UtcNow.Subtract(_offlineTimeout);
        var onlineComputers = await db.Computers
            .Where(c => c.Status == ComputerStatus.ONLINE && c.MachineName.ToLower() != currentMachineLower && c.LastHeartbeat.HasValue && c.LastHeartbeat < cutoffTime)
            .ToListAsync(stoppingToken);

        bool updated = false;

        foreach (var computer in onlineComputers)
        {
            computer.Status = ComputerStatus.OFFLINE;
            computer.UpdatedAt = DateTime.UtcNow;
            updated = true;

            _logger.LogWarning("Computador {MachineName} ({Id}) mudou de ONLINE para OFFLINE devido a ausência de heartbeat.", computer.MachineName, computer.Id);

            await eventService.CreateEventAsync(computer.Id, computer.AgentId, "AgentOffline", $"Computador {computer.MachineName} ficou OFFLINE (tempo limite de { (int)_offlineTimeout.TotalSeconds }s excedido).");
            
            if (computer.AgentId.HasValue)
            {
                var agent = await db.Agents.FirstOrDefaultAsync(a => a.Id == computer.AgentId.Value, stoppingToken);
                if (agent != null)
                {
                    agent.Status = AgentStatus.OFFLINE;
                    agent.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        // Sync local AnyDesk info (remote IDs & sessions) periodically
        try
        {
            var provider = new AnyDeskMonitor.Domain.Services.LocalAgentAnyDeskProvider();
            var anydeskInfo = await provider.GetAnyDeskInfoAsync(stoppingToken);

            if (anydeskInfo != null)
            {
                var comp = await db.Computers.FirstOrDefaultAsync(c => c.MachineName == currentMachine, stoppingToken);
                if (comp != null)
                {
                    if (!string.IsNullOrWhiteSpace(anydeskInfo.AnyDeskId))
                    {
                        comp.AnyDeskId = anydeskInfo.AnyDeskId;
                    }
                    var agent = await db.Agents.FirstOrDefaultAsync(a => a.ComputerId == comp.Id || a.MachineName == currentMachine, stoppingToken);

                    if (anydeskInfo.RemoteIds != null && anydeskInfo.RemoteIds.Any())
                    {
                        foreach (var r in anydeskInfo.RemoteIds)
                        {
                            if (!db.RemoteAnyDeskIds.Any(x => x.ComputerId == comp.Id && x.AnyDeskId == r.AnyDeskId))
                            {
                                var geo = AnyDeskMonitor.Domain.Services.GeoLocationService.ResolveLocation(r.AnyDeskId);
                                db.RemoteAnyDeskIds.Add(new RemoteAnyDeskId
                                {
                                    Id = Guid.NewGuid(),
                                    ComputerId = comp.Id,
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
                                updated = true;
                            }
                        }
                    }

                    if (anydeskInfo.Sessions != null && anydeskInfo.Sessions.Any())
                    {
                        foreach (var s in anydeskInfo.Sessions)
                        {
                            var dir = s.Direction ?? "ENTRADA";
                            if (!db.RemoteSessions.Any(x => x.ComputerId == comp.Id && x.RemoteAnyDeskId == s.RemoteAnyDeskId && x.InitiatedBy == dir))
                            {
                                var geo = AnyDeskMonitor.Domain.Services.GeoLocationService.ResolveLocation(s.RemoteAnyDeskId);
                                db.RemoteSessions.Add(new RemoteSession
                                {
                                    Id = Guid.NewGuid(),
                                    ComputerId = comp.Id,
                                    AgentId = agent?.Id,
                                    LocalAnyDeskId = s.LocalAnyDeskId ?? comp.AnyDeskId,
                                    RemoteAnyDeskId = s.RemoteAnyDeskId,
                                    Location = geo.Location,
                                    CountryCode = geo.CountryCode,
                                    CountryFlag = geo.CountryFlag,
                                    StartedAt = s.StartedAt,
                                    EndedAt = s.EndedAt,
                                    Status = SessionStatus.ACTIVE,
                                    InitiatedBy = dir
                                });
                                updated = true;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Erro ao sincronizar informações locais do AnyDesk.");
        }

        if (updated)
        {
            await db.SaveChangesAsync(stoppingToken);
            await _hubContext.Clients.All.SendAsync(SignalRConstants.Events.StatusChanged, "Status dos computadores atualizado.", cancellationToken: stoppingToken);
            await _hubContext.Clients.All.SendAsync(SignalRConstants.Events.DashboardUpdated, cancellationToken: stoppingToken);
        }
    }
}
