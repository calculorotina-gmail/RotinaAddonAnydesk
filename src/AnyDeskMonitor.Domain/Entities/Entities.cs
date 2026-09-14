using AnyDeskMonitor.Domain.Enums;

namespace AnyDeskMonitor.Domain.Entities;

public class Computer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string OperatingSystemVersion { get; set; } = string.Empty;
    public string IPAddress { get; set; } = string.Empty;
    public string? MacAddress { get; set; }
    public string? AnyDeskId { get; set; }
    public ComputerStatus Status { get; set; } = ComputerStatus.UNKNOWN;
    public Guid? AgentId { get; set; }
    public DateTime? LastHeartbeat { get; set; }
    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeen { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsEnabled { get; set; } = true;

    // Navigation
    public Agent? Agent { get; set; }
    public ICollection<RemoteAnyDeskId> RemoteAnyDeskIds { get; set; } = new List<RemoteAnyDeskId>();
    public ICollection<RemoteSession> RemoteSessions { get; set; } = new List<RemoteSession>();
    public ICollection<Event> Events { get; set; } = new List<Event>();
}

public class Agent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string InstallationId { get; set; } = string.Empty;
    public Guid? ComputerId { get; set; }
    public string Version { get; set; } = "1.0.0";
    public string MachineName { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string IPAddress { get; set; } = string.Empty;
    public DateTime? LastHeartbeat { get; set; }
    public AgentStatus Status { get; set; } = AgentStatus.ACTIVE;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeen { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? TokenHash { get; set; }

    // Navigation
    public Computer? Computer { get; set; }
}

public class RemoteAnyDeskId
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ComputerId { get; set; }
    public string AnyDeskId { get; set; } = string.Empty;
    public string? Alias { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? CountryCode { get; set; }
    public string? CountryFlag { get; set; }
    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeen { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsAuthorized { get; set; } = true;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Computer? Computer { get; set; }
}

public class RemoteAnyDeskIdHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RemoteAnyDeskIdId { get; set; }
    public Guid ComputerId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? PreviousValue { get; set; }
    public string? NewValue { get; set; }
    public string? ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }
}

public class RemoteSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ComputerId { get; set; }
    public Guid? AgentId { get; set; }
    public string? LocalAnyDeskId { get; set; }
    public string RemoteAnyDeskId { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? CountryCode { get; set; }
    public string? CountryFlag { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.ACTIVE;
    public string? InitiatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Computer? Computer { get; set; }
}

public class Event
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ComputerId { get; set; }
    public Guid? AgentId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? User { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Computer? Computer { get; set; }
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Operator;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLogin { get; set; }
}
