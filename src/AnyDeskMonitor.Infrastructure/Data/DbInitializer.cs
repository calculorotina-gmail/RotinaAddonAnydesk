using System.Net;
using System.Net.Sockets;
using AnyDeskMonitor.Domain.Entities;
using AnyDeskMonitor.Domain.Enums;
using AnyDeskMonitor.Domain.Interfaces;
using AnyDeskMonitor.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace AnyDeskMonitor.Infrastructure.Data;

public static class DbInitializer
{
    public static void Initialize(ApplicationDbContext context)
    {
        // 1. Garantir que a base de dados SQLite (C:\ProgramData\AnyDeskMonitor\anydeskmonitor.db) e todas as tabelas existem
        context.Database.EnsureCreated();

        // Limpar dados fictícios/mock (ex: IDs 123456789, 456789123 e AnyDeskId padrão 1738812731)
        CleanMockData(context);

        // 2. Obter dados reais da máquina atual e do AnyDesk local
        var currentMachineName = Environment.MachineName;
        var osVersion = Environment.OSVersion.VersionString;
        var localIp = GetLocalIpAddress();

        var provider = new LocalAgentAnyDeskProvider();
        var anydeskInfo = provider.GetAnyDeskInfoAsync().GetAwaiter().GetResult();

        var localAnyDeskId = !string.IsNullOrWhiteSpace(anydeskInfo?.AnyDeskId) 
            ? anydeskInfo.AnyDeskId 
            : null;

        var existingComputer = context.Computers.FirstOrDefault(c => c.MachineName == currentMachineName);

        if (existingComputer != null)
        {
            // Atualizar computador existente com estado online e dados do AnyDesk
            existingComputer.Status = ComputerStatus.ONLINE;
            existingComputer.LastSeen = DateTime.UtcNow;
            existingComputer.LastHeartbeat = DateTime.UtcNow;
            existingComputer.IPAddress = localIp;
            existingComputer.AnyDeskId = localAnyDeskId;

            var agent = context.Agents.FirstOrDefault(a => a.ComputerId == existingComputer.Id || a.MachineName == currentMachineName);
            if (agent != null)
            {
                agent.Status = AgentStatus.ACTIVE;
                agent.IsEnabled = true;
                agent.LastSeen = DateTime.UtcNow;
                agent.LastHeartbeat = DateTime.UtcNow;
                agent.IPAddress = localIp;
                existingComputer.AgentId = agent.Id;
            }
            else
            {
                var newAgentId = Guid.NewGuid();
                existingComputer.AgentId = newAgentId;
                agent = new Agent
                {
                    Id = newAgentId,
                    InstallationId = Guid.NewGuid().ToString(),
                    ComputerId = existingComputer.Id,
                    MachineName = currentMachineName,
                    OperatingSystem = "Windows",
                    IPAddress = localIp,
                    Version = "1.1.2",
                    Status = AgentStatus.ACTIVE,
                    IsEnabled = true,
                    RegisteredAt = DateTime.UtcNow,
                    LastSeen = DateTime.UtcNow,
                    LastHeartbeat = DateTime.UtcNow
                };
                context.Agents.Add(agent);
            }

            SyncAnyDeskInfoToContext(context, existingComputer.Id, agent.Id, localAnyDeskId, anydeskInfo);
            context.SaveChanges();
            return;
        }

        // 3. Se não existe computador registado para esta máquina, criar registo inicial
        var computerId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var currentComputer = new Computer
        {
            Id = computerId,
            Name = currentMachineName,
            MachineName = currentMachineName,
            OperatingSystem = "Windows 11 / Server",
            OperatingSystemVersion = osVersion,
            IPAddress = localIp,
            AnyDeskId = localAnyDeskId,
            Status = ComputerStatus.ONLINE,
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow,
            LastHeartbeat = DateTime.UtcNow,
            AgentId = agentId
        };
        context.Computers.Add(currentComputer);

        // 4. Criar agente registado para o computador atual
        var newAgent = new Agent
        {
            Id = agentId,
            InstallationId = Guid.NewGuid().ToString(),
            ComputerId = computerId,
            MachineName = currentMachineName,
            OperatingSystem = "Windows",
            IPAddress = localIp,
            Version = "1.1.2",
            Status = AgentStatus.ACTIVE,
            IsEnabled = true,
            RegisteredAt = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow,
            LastHeartbeat = DateTime.UtcNow
        };
        context.Agents.Add(newAgent);

        SyncAnyDeskInfoToContext(context, computerId, agentId, localAnyDeskId, anydeskInfo);

        // 5. Evento Inicial de Sistema
        context.Events.Add(new Event
        {
            Id = Guid.NewGuid(),
            ComputerId = computerId,
            AgentId = agentId,
            Type = "AgentRegistered",
            Message = $"Agente de produção ativado e registado no computador atual ({currentMachineName}).",
            CreatedAt = DateTime.UtcNow
        });

        // 6. Guardar tudo na base de dados SQLite
        context.SaveChanges();
    }

    private static void CleanMockData(ApplicationDbContext context)
    {
        try
        {
            var dummyIds = new[] { "123456789", "456789123" };
            var dummyRemoteRecords = context.RemoteAnyDeskIds.Where(x => dummyIds.Contains(x.AnyDeskId)).ToList();
            if (dummyRemoteRecords.Any())
            {
                context.RemoteAnyDeskIds.RemoveRange(dummyRemoteRecords);
            }

            var dummySessions = context.RemoteSessions.Where(x => 
                dummyIds.Contains(x.RemoteAnyDeskId) || 
                x.LocalAnyDeskId == "1738812731" || 
                x.LocalAnyDeskId == "000000000").ToList();

            if (dummySessions.Any())
            {
                context.RemoteSessions.RemoveRange(dummySessions);
            }

            var provider = new LocalAgentAnyDeskProvider();
            var realInfo = provider.GetAnyDeskInfoAsync().GetAwaiter().GetResult();

            if (realInfo != null && !string.IsNullOrWhiteSpace(realInfo.AnyDeskId))
            {
                var computersWithDummyId = context.Computers.Where(c => c.AnyDeskId == "1738812731" || c.AnyDeskId == "000000000" || string.IsNullOrEmpty(c.AnyDeskId)).ToList();
                foreach (var comp in computersWithDummyId)
                {
                    comp.AnyDeskId = realInfo.AnyDeskId;
                }
            }

            context.SaveChanges();
        }
        catch
        {
            // Silently skip clean errors
        }
    }

    private static void SyncAnyDeskInfoToContext(ApplicationDbContext context, Guid computerId, Guid agentId, string? localAnyDeskId, AnyDeskInfo? anydeskInfo)
    {
        // Guardar IDs Remotos
        var remoteIdsToSave = new List<RemoteAnyDeskId>();

        if (anydeskInfo?.RemoteIds != null && anydeskInfo.RemoteIds.Any())
        {
            foreach (var r in anydeskInfo.RemoteIds)
            {
                if (!context.RemoteAnyDeskIds.Any(x => x.ComputerId == computerId && x.AnyDeskId == r.AnyDeskId))
                {
                    var geo = AnyDeskMonitor.Domain.Services.GeoLocationService.ResolveLocation(r.AnyDeskId);
                    remoteIdsToSave.Add(new RemoteAnyDeskId
                    {
                        Id = Guid.NewGuid(),
                        ComputerId = computerId,
                        AnyDeskId = r.AnyDeskId,
                        Alias = r.Alias ?? "Detetado no Sistema",
                        Description = "ID AnyDesk detetado nas configurações ou trace",
                        Location = geo.Location,
                        CountryCode = geo.CountryCode,
                        CountryFlag = geo.CountryFlag,
                        FirstSeen = r.LastSeen,
                        LastSeen = DateTime.UtcNow,
                        IsActive = true,
                        IsAuthorized = true
                    });
                }
            }
        }

        if (remoteIdsToSave.Any())
        {
            context.RemoteAnyDeskIds.AddRange(remoteIdsToSave);
        }

        // Guardar Sessões Remotas
        var sessionsToSave = new List<RemoteSession>();

        if (anydeskInfo?.Sessions != null && anydeskInfo.Sessions.Any())
        {
            foreach (var s in anydeskInfo.Sessions)
            {
                var dir = s.Direction ?? "ENTRADA";
                if (!context.RemoteSessions.Any(x => x.ComputerId == computerId && x.RemoteAnyDeskId == s.RemoteAnyDeskId && x.InitiatedBy == dir))
                {
                    var geo = AnyDeskMonitor.Domain.Services.GeoLocationService.ResolveLocation(s.RemoteAnyDeskId);
                    sessionsToSave.Add(new RemoteSession
                    {
                        Id = Guid.NewGuid(),
                        ComputerId = computerId,
                        AgentId = agentId,
                        LocalAnyDeskId = s.LocalAnyDeskId ?? localAnyDeskId,
                        RemoteAnyDeskId = s.RemoteAnyDeskId,
                        Location = geo.Location,
                        CountryCode = geo.CountryCode,
                        CountryFlag = geo.CountryFlag,
                        StartedAt = s.StartedAt,
                        EndedAt = s.EndedAt,
                        Status = SessionStatus.COMPLETED,
                        InitiatedBy = dir
                    });
                }
            }
        }

        if (sessionsToSave.Any())
        {
            context.RemoteSessions.AddRange(sessionsToSave);
        }
    }

    private static string GetLocalIpAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            var endPoint = socket.LocalEndPoint as IPEndPoint;
            return endPoint?.Address.ToString() ?? "127.0.0.1";
        }
        catch
        {
            return "127.0.0.1";
        }
    }
}
