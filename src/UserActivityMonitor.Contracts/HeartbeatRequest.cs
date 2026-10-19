namespace UserActivityMonitor.Contracts;

public record HeartbeatRequest(
    Guid ClientId, 
    string DomainName,
    string MachineName,
    string UserName,
    DateTimeOffset? LastInputAt);