namespace UserActivityMonitor.Contracts;

public record ClientInfo(
    Guid ClientId,
    string DomainName,
    string MachineName,
    string UserName,
    string? RemoteIpAddress,
    DateTimeOffset? LastInputAt,
    DateTimeOffset LastSeenAt,
    bool IsOnline);