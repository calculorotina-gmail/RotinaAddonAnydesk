using System.Net.Http.Json;
using AnyDeskMonitor.Shared.DTOs;
using Microsoft.Extensions.Logging;

namespace AnyDeskMonitor.Agent.Services;

public class ApiClientService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiClientService> _logger;

    public ApiClientService(HttpClient httpClient, ILogger<ApiClientService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<AgentStatusDto?> RegisterAsync(string serverUrl, AgentRegistrationDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{serverUrl.TrimEnd('/')}/api/agents/register";
            var response = await _httpClient.PostAsJsonAsync(url, dto, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<AgentStatusDto>(cancellationToken: cancellationToken);
            }

            _logger.LogWarning("Falha no registo do agente. Código HTTP: {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro de comunicação HTTP ao registar agente.");
        }
        return null;
    }

    public async Task<AgentStatusDto?> SendHeartbeatAsync(string serverUrl, HeartbeatDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{serverUrl.TrimEnd('/')}/api/agents/heartbeat";
            var response = await _httpClient.PostAsJsonAsync(url, dto, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<AgentStatusDto>(cancellationToken: cancellationToken);
            }

            _logger.LogWarning("Falha no envio de heartbeat. Código HTTP: {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro de comunicação HTTP no heartbeat.");
        }
        return null;
    }
}

public class AgentRegistrationService
{
    private readonly ApiClientService _apiClient;
    private readonly ComputerInfoService _computerInfo;
    private readonly AnyDeskInfoService _anyDeskInfo;
    private readonly ILogger<AgentRegistrationService> _logger;

    public AgentRegistrationService(
        ApiClientService apiClient,
        ComputerInfoService computerInfo,
        AnyDeskInfoService anyDeskInfo,
        ILogger<AgentRegistrationService> logger)
    {
        _apiClient = apiClient;
        _computerInfo = computerInfo;
        _anyDeskInfo = anyDeskInfo;
        _logger = logger;
    }

    public async Task<bool> EnsureRegisteredAsync(AgentConfig config, CancellationToken cancellationToken = default)
    {
        var anydeskInfo = await _anyDeskInfo.GetAnyDeskInfoAsync(cancellationToken);

        var dto = new AgentRegistrationDto
        {
            InstallationId = config.InstallationId,
            MachineName = _computerInfo.GetMachineName(),
            OperatingSystem = _computerInfo.GetOperatingSystem(),
            OperatingSystemVersion = _computerInfo.GetOperatingSystemVersion(),
            IPAddress = _computerInfo.GetLocalIpAddress(),
            Version = "1.1.2",
            AnyDeskId = anydeskInfo?.AnyDeskId
        };

        if (anydeskInfo?.RemoteIds != null)
        {
            foreach (var r in anydeskInfo.RemoteIds)
            {
                dto.RemoteIds.Add(new CreateRemoteAnyDeskIdDto
                {
                    AnyDeskId = r.AnyDeskId,
                    Alias = r.Alias,
                    Description = "Detetado automaticamente pelo Agente"
                });
            }
        }

        if (anydeskInfo?.Sessions != null)
        {
            foreach (var s in anydeskInfo.Sessions)
            {
                dto.RemoteSessions.Add(new RemoteSessionDto
                {
                    LocalAnyDeskId = s.LocalAnyDeskId,
                    RemoteAnyDeskId = s.RemoteAnyDeskId,
                    StartedAt = s.StartedAt,
                    EndedAt = s.EndedAt,
                    Status = s.Status,
                    InitiatedBy = s.Direction ?? "DESCONHECIDO"
                });
            }
        }

        var result = await _apiClient.RegisterAsync(config.ServerUrl, dto, cancellationToken);
        if (result != null)
        {
            config.AgentId = result.Id.ToString();
            config.Save();
            _logger.LogInformation("Agente registado com sucesso na API. AgentId: {AgentId}", result.Id);
            return true;
        }

        return false;
    }
}

public class HeartbeatService
{
    private readonly ApiClientService _apiClient;
    private readonly ComputerInfoService _computerInfo;
    private readonly AnyDeskInfoService _anyDeskInfo;
    private readonly AgentRegistrationService _registrationService;
    private readonly ILogger<HeartbeatService> _logger;

    public HeartbeatService(
        ApiClientService apiClient,
        ComputerInfoService computerInfo,
        AnyDeskInfoService anyDeskInfo,
        AgentRegistrationService registrationService,
        ILogger<HeartbeatService> logger)
    {
        _apiClient = apiClient;
        _computerInfo = computerInfo;
        _anyDeskInfo = anyDeskInfo;
        _registrationService = registrationService;
        _logger = logger;
    }

    public async Task<bool> SendHeartbeatAsync(AgentConfig config, CancellationToken cancellationToken = default)
    {
        var anydeskInfo = await _anyDeskInfo.GetAnyDeskInfoAsync(cancellationToken);

        var dto = new HeartbeatDto
        {
            InstallationId = config.InstallationId,
            MachineName = _computerInfo.GetMachineName(),
            OperatingSystem = _computerInfo.GetOperatingSystem(),
            IPAddress = _computerInfo.GetLocalIpAddress(),
            Version = "1.1.2",
            AnyDeskId = anydeskInfo?.AnyDeskId
        };

        if (anydeskInfo?.RemoteIds != null)
        {
            foreach (var r in anydeskInfo.RemoteIds)
            {
                dto.RemoteIds.Add(new CreateRemoteAnyDeskIdDto
                {
                    AnyDeskId = r.AnyDeskId,
                    Alias = r.Alias,
                    Description = "Detetado automaticamente pelo Agente"
                });
            }
        }

        if (anydeskInfo?.Sessions != null)
        {
            foreach (var s in anydeskInfo.Sessions)
            {
                dto.RemoteSessions.Add(new RemoteSessionDto
                {
                    LocalAnyDeskId = s.LocalAnyDeskId,
                    RemoteAnyDeskId = s.RemoteAnyDeskId,
                    StartedAt = s.StartedAt,
                    EndedAt = s.EndedAt,
                    Status = s.Status,
                    InitiatedBy = s.Direction ?? "DESCONHECIDO"
                });
            }
        }

        var result = await _apiClient.SendHeartbeatAsync(config.ServerUrl, dto, cancellationToken);
        if (result != null)
        {
            _logger.LogInformation("Heartbeat enviado com sucesso para {MachineName} às {Time}", dto.MachineName, DateTime.Now.ToLongTimeString());
            return true;
        }

        _logger.LogWarning("Falha no envio do heartbeat. Tentando re-registar agente...");
        return await _registrationService.EnsureRegisteredAsync(config, cancellationToken);
    }
}

public class RemoteIdSyncService
{
    private readonly ILogger<RemoteIdSyncService> _logger;

    public RemoteIdSyncService(ILogger<RemoteIdSyncService> logger)
    {
        _logger = logger;
    }

    public async Task SyncAsync(AgentConfig config, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sincronização manual do agente {InstallationId} executada.", config.InstallationId);
        await Task.CompletedTask;
    }
}
