namespace UserActivityMonitor.Server;

enum ScreenshotUpdateResult
{
    Stored,
    NotFound,
    WrongClient,
    NotPending,
    Expired
}
