using UserActivityMonitor.Contracts;

namespace UserActivityMonitor.Server;

class ScreenshotEntry(ScreenshotRequestInfo info)
{
    public ScreenshotRequestInfo Info { get; set; } = info;
    public byte[]? Image { get; set; }
}
