namespace UserActivityMonitor.Contracts;

public record ScreenshotRequestInfo(
    Guid RequestId,
    Guid ClientId,
    ScreenshotState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string? Error);
