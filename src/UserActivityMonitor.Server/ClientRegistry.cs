using UserActivityMonitor.Contracts;

namespace UserActivityMonitor.Server;

public class ClientRegistry(TimeProvider timeProvider)
{
    private static readonly TimeSpan OfflineTimout = TimeSpan.FromSeconds(20);
    private readonly Lock _sync = new();
    private readonly Dictionary<Guid, ClientInfo> _clients = new();

    public void RecordHeartbeat(HeartbeatRequest heartbeat, string remoteIpAddress)
    {
        lock (_sync)
        {
            _clients[heartbeat.ClientId] = new ClientInfo(
                heartbeat.ClientId,
                heartbeat.DomainName,
                heartbeat.MachineName,
                heartbeat.UserName,
                remoteIpAddress,
                heartbeat.LastInputAt?.ToUniversalTime(),
                timeProvider.GetUtcNow(),
                IsOnline: true);
        }
    }

    public ClientInfo[] GetClients()
    {
        lock (_sync)
        {
            var now = timeProvider.GetUtcNow();

            return _clients.Values.Select(client => client with
                {
                    IsOnline = now - client.LastSeenAt < OfflineTimout
                })
                .OrderBy(client => client.MachineName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(client => client.UserName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(client => client.ClientId)
                .ToArray();
        }
    }
}