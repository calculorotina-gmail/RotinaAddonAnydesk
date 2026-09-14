namespace AnyDeskMonitor.Domain.Enums;

public enum ComputerStatus
{
    UNKNOWN = 0,
    ONLINE = 1,
    OFFLINE = 2
}

public enum AgentStatus
{
    ACTIVE = 1,
    INACTIVE = 2,
    OFFLINE = 3
}

public enum UserRole
{
    Administrator = 1,
    Operator = 2,
    Viewer = 3
}

public enum RemoteIdStatus
{
    ACTIVE = 1,
    INACTIVE = 2,
    PENDING = 3
}

public enum SessionStatus
{
    ACTIVE = 1,
    COMPLETED = 2,
    FAILED = 3,
    TERMINATED = 4
}
