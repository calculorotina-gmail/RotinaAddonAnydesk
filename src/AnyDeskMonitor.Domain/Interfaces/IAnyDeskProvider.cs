namespace AnyDeskMonitor.Domain.Interfaces;

public record DiscoveredRemoteIdInfo(string AnyDeskId, string? Alias, DateTime LastSeen);
public record DiscoveredSessionInfo(string LocalAnyDeskId, string RemoteAnyDeskId, DateTime StartedAt, DateTime? EndedAt, string Status, string? Direction);

public record AnyDeskInfo(
    string? AnyDeskId,
    string? Alias,
    string? SourcePath,
    List<DiscoveredRemoteIdInfo>? RemoteIds = null,
    List<DiscoveredSessionInfo>? Sessions = null
);

public interface IAnyDeskProvider
{
    Task<AnyDeskInfo?> GetAnyDeskInfoAsync(CancellationToken cancellationToken = default);
}
