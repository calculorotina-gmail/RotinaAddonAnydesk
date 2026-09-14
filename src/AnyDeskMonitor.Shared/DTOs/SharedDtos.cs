namespace AnyDeskMonitor.Shared.DTOs;

public class AgentRegistrationDto
{
    public string InstallationId { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string OperatingSystemVersion { get; set; } = string.Empty;
    public string IPAddress { get; set; } = string.Empty;
    public string Version { get; set; } = "1.1.2";
    public string? AnyDeskId { get; set; }
    public List<CreateRemoteAnyDeskIdDto> RemoteIds { get; set; } = new();
    public List<RemoteSessionDto> RemoteSessions { get; set; } = new();
}

public class HeartbeatDto
{
    public string InstallationId { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string IPAddress { get; set; } = string.Empty;
    public string Version { get; set; } = "1.1.2";
    public string? AnyDeskId { get; set; }
    public List<CreateRemoteAnyDeskIdDto> RemoteIds { get; set; } = new();
    public List<RemoteSessionDto> RemoteSessions { get; set; } = new();
}

public class ComputerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string OperatingSystemVersion { get; set; } = string.Empty;
    public string IPAddress { get; set; } = string.Empty;
    public string? MacAddress { get; set; }
    public string? AnyDeskId { get; set; }
    public string Status { get; set; } = "UNKNOWN";
    public Guid? AgentId { get; set; }
    public DateTime? LastHeartbeat { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime? LastSeen { get; set; }
    public bool IsEnabled { get; set; }
}

public class RemoteAnyDeskIdDto
{
    public Guid Id { get; set; }
    public Guid ComputerId { get; set; }
    public string AnyDeskId { get; set; } = string.Empty;
    public string? Alias { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? CountryCode { get; set; }
    public string? CountryFlag { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime? LastSeen { get; set; }
    public bool IsActive { get; set; }
    public bool IsAuthorized { get; set; }
    public string? Notes { get; set; }
}

public class CreateRemoteAnyDeskIdDto
{
    public Guid ComputerId { get; set; }
    public string AnyDeskId { get; set; } = string.Empty;
    public string? Alias { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? CountryCode { get; set; }
    public string? CountryFlag { get; set; }
    public string? Notes { get; set; }
}

public class RemoteSessionDto
{
    public Guid Id { get; set; }
    public Guid ComputerId { get; set; }
    public Guid? AgentId { get; set; }
    public string? LocalAnyDeskId { get; set; }
    public string RemoteAnyDeskId { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? CountryCode { get; set; }
    public string? CountryFlag { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string? Duration { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public string? InitiatedBy { get; set; }
}

public class LoginDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class DashboardStatsDto
{
    public int TotalComputers { get; set; }
    public int OnlineComputers { get; set; }
    public int OfflineComputers { get; set; }
    public int UnknownComputers { get; set; }
    public int ActiveAgents { get; set; }
    public int InactiveAgents { get; set; }
    public int LocalAnyDeskIds { get; set; }
    public int RemoteAnyDeskIds { get; set; }
    public int ActiveSessions { get; set; }
    public int SessionsToday { get; set; }
}

public class AgentStatusDto
{
    public Guid Id { get; set; }
    public string InstallationId { get; set; } = string.Empty;
    public Guid? ComputerId { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string IPAddress { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Status { get; set; } = "ACTIVE";
    public DateTime? LastHeartbeat { get; set; }
    public bool IsEnabled { get; set; }
}

public class EventDto
{
    public Guid Id { get; set; }
    public Guid? ComputerId { get; set; }
    public Guid? AgentId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? User { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AuditDto
{
    public Guid Id { get; set; }
    public string User { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public DateTime Timestamp { get; set; }
    public string? IPAddress { get; set; }
    public string? Details { get; set; }
}
