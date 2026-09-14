using AnyDeskMonitor.Shared.DTOs;

namespace AnyDeskMonitor.Application.Interfaces;

public interface IComputerService
{
    Task<IEnumerable<ComputerDto>> GetAllComputersAsync(string? search = null, string? status = null, string? os = null);
    Task<ComputerDto?> GetComputerByIdAsync(Guid id);
    Task<ComputerDto?> UpdateComputerAsync(Guid id, ComputerDto dto);
    Task<bool> DeleteComputerAsync(Guid id);
    Task<bool> SyncComputerAsync(Guid id);
    Task<IEnumerable<RemoteAnyDeskIdDto>> GetRemoteIdsByComputerIdAsync(Guid computerId);
    Task<IEnumerable<EventDto>> GetEventsByComputerIdAsync(Guid computerId);
}

public interface IAgentService
{
    Task<AgentStatusDto> RegisterAgentAsync(AgentRegistrationDto dto);
    Task<AgentStatusDto?> ProcessHeartbeatAsync(HeartbeatDto dto);
    Task<IEnumerable<AgentStatusDto>> GetAllAgentsAsync();
    Task<AgentStatusDto?> GetAgentByIdAsync(Guid id);
    Task<bool> EnableAgentAsync(Guid id);
    Task<bool> DisableAgentAsync(Guid id);
    Task<bool> SyncAgentAsync(Guid id);
}

public interface IAuthService
{
    Task<LoginResponseDto?> LoginAsync(LoginDto dto);
    Task SeedDefaultUsersAsync();
}

public interface IDashboardService
{
    Task<DashboardStatsDto> GetStatsAsync();
}

public interface IEventService
{
    Task<IEnumerable<EventDto>> GetEventsAsync(string? type = null, Guid? computerId = null, int limit = 100);
    Task CreateEventAsync(Guid? computerId, Guid? agentId, string type, string message, string? user = null);
}

public interface IRemoteIdService
{
    Task<IEnumerable<RemoteAnyDeskIdDto>> GetAllRemoteIdsAsync();
    Task<RemoteAnyDeskIdDto> CreateRemoteIdAsync(CreateRemoteAnyDeskIdDto dto, string? user = null);
    Task<RemoteAnyDeskIdDto?> UpdateRemoteIdAsync(Guid id, RemoteAnyDeskIdDto dto, string? user = null);
    Task<bool> DeleteRemoteIdAsync(Guid id, string? user = null);
    Task<IEnumerable<AuditDto>> GetHistoryAsync(Guid? remoteId = null);
}

public interface ISessionService
{
    Task<IEnumerable<RemoteSessionDto>> GetAllSessionsAsync();
    Task<RemoteSessionDto?> GetSessionByIdAsync(Guid id);
    Task RecordSessionAsync(Guid computerId, Guid? agentId, string remoteAnyDeskId, string? localAnyDeskId, string? initiatedBy = null);
    Task<bool> KillSessionAsync(Guid id, string? user = null);
}

public interface IAuditService
{
    Task<IEnumerable<AuditDto>> GetAuditLogsAsync(int limit = 100);
    Task LogAuditAsync(string user, string action, string entity, string? entityId = null, string? ipAddress = null, string? details = null);
}
