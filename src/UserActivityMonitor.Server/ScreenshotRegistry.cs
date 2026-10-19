using UserActivityMonitor.Contracts;

namespace UserActivityMonitor.Server;

class ScreenshotRegistry(TimeProvider timeProvider)
{
    public const int MaxImageBytes = 5 * 1024 * 1024;
    const int MaxRequests = 32;
    static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
    static readonly TimeSpan Retention = TimeSpan.FromMinutes(5);
    readonly Lock _sync = new();
    readonly Dictionary<Guid, ScreenshotEntry> _requests = new();

    public ScreenshotRequestInfo? Create(Guid clientId)
    {
        lock (_sync)
        {
            Cleanup();
            var pending = _requests.Values.FirstOrDefault(entry =>
                entry.Info.ClientId == clientId && entry.Info.State == ScreenshotState.Pending);
            if (pending is not null) return pending.Info;
            if (_requests.Count >= MaxRequests) return null;

            var now = timeProvider.GetUtcNow();
            var info = new ScreenshotRequestInfo(Guid.NewGuid(), clientId,
                ScreenshotState.Pending, now, now + RequestTimeout, null);
            _requests.Add(info.RequestId, new ScreenshotEntry(info));
            return info;
        }
    }

    public ScreenshotRequestInfo? Get(Guid requestId)
    {
        lock (_sync)
        {
            Cleanup();
            return _requests.GetValueOrDefault(requestId)?.Info;
        }
    }

    public ScreenshotCommand? GetPendingCommand(Guid clientId)
    {
        lock (_sync)
        {
            Cleanup();
            var info = _requests.Values.FirstOrDefault(entry =>
                entry.Info.ClientId == clientId && entry.Info.State == ScreenshotState.Pending)?.Info;
            // Redelivery allows the next heartbeat to recover a lost response.
            return info is null ? null : new ScreenshotCommand(info.RequestId, info.ExpiresAt);
        }
    }

    public byte[]? GetImage(Guid requestId)
    {
        lock (_sync)
        {
            Cleanup();
            return _requests.GetValueOrDefault(requestId)?.Image?.ToArray();
        }
    }

    public ScreenshotUpdateResult Complete(Guid clientId, Guid requestId, byte[] image)
    {
        if (image.Length == 0 || image.Length > MaxImageBytes)
            throw new ArgumentOutOfRangeException(nameof(image));
        return Finish(clientId, requestId, image, null);
    }

    public ScreenshotUpdateResult Fail(Guid clientId, Guid requestId, string error)
    {
        if (string.IsNullOrWhiteSpace(error) || error.Length > 1000)
            throw new ArgumentException("Error must contain 1 to 1000 characters.", nameof(error));
        return Finish(clientId, requestId, null, error.Trim());
    }

    ScreenshotUpdateResult Finish(Guid clientId, Guid requestId, byte[]? image, string? error)
    {
        lock (_sync)
        {
            Cleanup();
            if (!_requests.TryGetValue(requestId, out var entry)) return ScreenshotUpdateResult.NotFound;
            if (entry.Info.ClientId != clientId) return ScreenshotUpdateResult.WrongClient;
            if (entry.Info.State == ScreenshotState.Expired) return ScreenshotUpdateResult.Expired;
            if (entry.Info.State != ScreenshotState.Pending) return ScreenshotUpdateResult.NotPending;

            entry.Image = image?.ToArray();
            entry.Info = entry.Info with
            {
                State = image is null ? ScreenshotState.Failed : ScreenshotState.Completed,
                Error = error
            };
            return ScreenshotUpdateResult.Stored;
        }
    }

    // Cleanup is lazy: every registry operation expires and removes old entries.
    void Cleanup()
    {
        var now = timeProvider.GetUtcNow();
        foreach (var (id, entry) in _requests.ToArray())
        {
            if (now >= entry.Info.CreatedAt + Retention)
                _requests.Remove(id);
            else if (entry.Info.State == ScreenshotState.Pending && now >= entry.Info.ExpiresAt)
                entry.Info = entry.Info with { State = ScreenshotState.Expired };
        }
    }
}
