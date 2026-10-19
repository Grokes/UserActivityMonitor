using System.Text.Json.Serialization;

namespace UserActivityMonitor.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<ScreenshotState>))]
public enum ScreenshotState
{
    Pending,
    Completed,
    Failed,
    Expired
}
