namespace UserActivityMonitor.Client;

record PendingScreenshot(Guid RequestId, byte[]? Image, string? Error);
