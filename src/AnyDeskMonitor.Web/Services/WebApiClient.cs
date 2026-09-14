using System.Net.Http.Json;
using AnyDeskMonitor.Application.Interfaces;
using AnyDeskMonitor.Shared.DTOs;
using Microsoft.Extensions.DependencyInjection;

namespace AnyDeskMonitor.Web.Services;

public class WebApiClient
{
    private readonly HttpClient _http;
    private readonly IServiceProvider _serviceProvider;
    private string? _jwtToken;

    public WebApiClient(HttpClient http, IConfiguration config, IServiceProvider serviceProvider)
    {
        _http = http;
        _serviceProvider = serviceProvider;
        var baseUrl = config["API_URL"] ?? "http://localhost:5000";
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(3);
    }

    public void SetToken(string token)
    {
        _jwtToken = token;
        _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<LoginResponseDto?> LoginAsync(LoginDto dto)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/auth/login", dto);
            if (resp.IsSuccessStatusCode)
            {
                var result = await resp.Content.ReadFromJsonAsync<LoginResponseDto>();
                if (result != null) SetToken(result.Token);
                return result;
            }
        }
        catch { }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var authService = scope.ServiceProvider.GetService<IAuthService>();
            if (authService != null)
            {
                return await authService.LoginAsync(dto);
            }
        }
        catch { }

        return null;
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync()
    {
        try
        {
            var stats = await _http.GetFromJsonAsync<DashboardStatsDto>("api/dashboard");
            if (stats != null && stats.TotalComputers > 0) return stats;
        }
        catch { }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetService<IDashboardService>();
            if (service != null)
            {
                return await service.GetStatsAsync();
            }
        }
        catch { }

        return new DashboardStatsDto();
    }

    public async Task<List<ComputerDto>> GetComputersAsync(string? search = null, string? status = null, string? os = null)
    {
        try
        {
            var query = $"api/computers?search={Uri.EscapeDataString(search ?? "")}&status={Uri.EscapeDataString(status ?? "")}&os={Uri.EscapeDataString(os ?? "")}";
            var result = await _http.GetFromJsonAsync<List<ComputerDto>>(query);
            if (result != null) return result;
        }
        catch { }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetService<IComputerService>();
            if (service != null)
            {
                var computers = await service.GetAllComputersAsync(search, status, os);
                return computers.ToList();
            }
        }
        catch { }

        return new List<ComputerDto>();
    }

    public async Task<ComputerDto?> GetComputerByIdAsync(Guid id)
    {
        try
        {
            var result = await _http.GetFromJsonAsync<ComputerDto>($"api/computers/{id}");
            if (result != null) return result;
        }
        catch { }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetService<IComputerService>();
            if (service != null)
            {
                return await service.GetComputerByIdAsync(id);
            }
        }
        catch { }

        return null;
    }

    public async Task<List<RemoteAnyDeskIdDto>> GetRemoteIdsByComputerIdAsync(Guid computerId)
    {
        try
        {
            var result = await _http.GetFromJsonAsync<List<RemoteAnyDeskIdDto>>($"api/computers/{computerId}/remoteids");
            if (result != null) return result;
        }
        catch { }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetService<IComputerService>();
            if (service != null)
            {
                var list = await service.GetRemoteIdsByComputerIdAsync(computerId);
                return list.ToList();
            }
        }
        catch { }

        return new List<RemoteAnyDeskIdDto>();
    }

    public async Task<List<EventDto>> GetEventsByComputerIdAsync(Guid computerId)
    {
        try
        {
            var result = await _http.GetFromJsonAsync<List<EventDto>>($"api/computers/{computerId}/events");
            if (result != null) return result;
        }
        catch { }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetService<IComputerService>();
            if (service != null)
            {
                var list = await service.GetEventsByComputerIdAsync(computerId);
                return list.ToList();
            }
        }
        catch { }

        return new List<EventDto>();
    }

    public async Task<List<AgentStatusDto>> GetAgentsAsync()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<List<AgentStatusDto>>("api/agents");
            if (result != null) return result;
        }
        catch { }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetService<IAgentService>();
            if (service != null)
            {
                var list = await service.GetAllAgentsAsync();
                return list.ToList();
            }
        }
        catch { }

        return new List<AgentStatusDto>();
    }

    public async Task<List<RemoteAnyDeskIdDto>> GetRemoteIdsAsync()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<List<RemoteAnyDeskIdDto>>("api/remoteids");
            if (result != null) return result;
        }
        catch { }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetService<IRemoteIdService>();
            if (service != null)
            {
                var list = await service.GetAllRemoteIdsAsync();
                return list.ToList();
            }
        }
        catch { }

        return new List<RemoteAnyDeskIdDto>();
    }

    public async Task<RemoteAnyDeskIdDto?> CreateRemoteIdAsync(CreateRemoteAnyDeskIdDto dto)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/remoteids", dto);
            if (resp.IsSuccessStatusCode)
            {
                return await resp.Content.ReadFromJsonAsync<RemoteAnyDeskIdDto>();
            }
        }
        catch { }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetService<IRemoteIdService>();
            if (service != null)
            {
                return await service.CreateRemoteIdAsync(dto, "Admin");
            }
        }
        catch { }

        return null;
    }

    public async Task<bool> DeleteRemoteIdAsync(Guid id)
    {
        try
        {
            var resp = await _http.DeleteAsync($"api/remoteids/{id}");
            if (resp.IsSuccessStatusCode) return true;
        }
        catch { }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetService<IRemoteIdService>();
            if (service != null)
            {
                return await service.DeleteRemoteIdAsync(id, "Admin");
            }
        }
        catch { }

        return false;
    }

    public async Task<RemoteAnyDeskIdDto?> UpdateRemoteIdAsync(Guid id, RemoteAnyDeskIdDto dto)
    {
        try
        {
            var resp = await _http.PutAsJsonAsync($"api/remoteids/{id}", dto);
            if (resp.IsSuccessStatusCode)
            {
                return await resp.Content.ReadFromJsonAsync<RemoteAnyDeskIdDto>();
            }
        }
        catch { }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetService<IRemoteIdService>();
            if (service != null)
            {
                return await service.UpdateRemoteIdAsync(id, dto, "Admin");
            }
        }
        catch { }

        return null;
    }

    public async Task<List<RemoteSessionDto>> GetSessionsAsync()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<List<RemoteSessionDto>>("api/sessions");
            if (result != null) return result;
        }
        catch { }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetService<ISessionService>();
            if (service != null)
            {
                var list = await service.GetAllSessionsAsync();
                return list.ToList();
            }
        }
        catch { }

        return new List<RemoteSessionDto>();
    }

    public async Task<bool> KillSessionAsync(Guid id)
    {
        try
        {
            var resp = await _http.PostAsync($"api/sessions/{id}/taskkill", null);
            if (resp.IsSuccessStatusCode) return true;
        }
        catch { }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetService<ISessionService>();
            if (service != null)
            {
                return await service.KillSessionAsync(id, "Admin");
            }
        }
        catch { }

        return false;
    }

    public async Task<List<EventDto>> GetEventsAsync(string? type = null, Guid? computerId = null)
    {
        try
        {
            var result = await _http.GetFromJsonAsync<List<EventDto>>($"api/events?type={type}&computerId={computerId}");
            if (result != null) return result;
        }
        catch { }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetService<IEventService>();
            if (service != null)
            {
                var list = await service.GetEventsAsync(type, computerId);
                return list.ToList();
            }
        }
        catch { }

        return new List<EventDto>();
    }

    public async Task<List<AuditDto>> GetAuditLogsAsync()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<List<AuditDto>>("api/audit");
            if (result != null) return result;
        }
        catch { }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetService<IAuditService>();
            if (service != null)
            {
                var list = await service.GetAuditLogsAsync();
                return list.ToList();
            }
        }
        catch { }

        return new List<AuditDto>();
    }

    public async Task<bool> EnableAgentAsync(Guid id)
    {
        try
        {
            var resp = await _http.PutAsync($"api/agents/{id}/enable", null);
            if (resp.IsSuccessStatusCode) return true;
        }
        catch { }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetService<IAgentService>();
            if (service != null)
            {
                return await service.EnableAgentAsync(id);
            }
        }
        catch { }

        return false;
    }

    public async Task<bool> DisableAgentAsync(Guid id)
    {
        try
        {
            var resp = await _http.PutAsync($"api/agents/{id}/disable", null);
            if (resp.IsSuccessStatusCode) return true;
        }
        catch { }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetService<IAgentService>();
            if (service != null)
            {
                return await service.DisableAgentAsync(id);
            }
        }
        catch { }

        return false;
    }

    public async Task<bool> SyncAgentAsync(Guid id)
    {
        try
        {
            var resp = await _http.PostAsync($"api/agents/{id}/sync", null);
            if (resp.IsSuccessStatusCode) return true;
        }
        catch { }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetService<IAgentService>();
            if (service != null)
            {
                return await service.SyncAgentAsync(id);
            }
        }
        catch { }

        return false;
    }

    public async Task<bool> DeleteComputerAsync(Guid id)
    {
        try
        {
            var resp = await _http.DeleteAsync($"api/computers/{id}");
            if (resp.IsSuccessStatusCode) return true;
        }
        catch { }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetService<IComputerService>();
            if (service != null)
            {
                return await service.DeleteComputerAsync(id);
            }
        }
        catch { }

        return false;
    }

    public async Task<bool> SyncComputerAsync(Guid id)
    {
        try
        {
            var resp = await _http.PostAsync($"api/computers/{id}/sync", null);
            if (resp.IsSuccessStatusCode) return true;
        }
        catch { }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetService<IComputerService>();
            if (service != null)
            {
                return await service.SyncComputerAsync(id);
            }
        }
        catch { }

        return false;
    }
}
