namespace UserActivityMonitor.Contracts;

public record ScreenshotCommand(Guid RequestId, DateTimeOffset ExpiresAt);
