using System.Runtime.InteropServices;

namespace UserActivityMonitor.Client;

public static class InputReader
{
    public static DateTimeOffset? ReadUtc()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info))
            return null;

        // Both values use the low 32 bits of the tick count; subtraction must wrap.
        var idleMilliseconds = unchecked((uint)Environment.TickCount - info.Time);
        return DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(idleMilliseconds);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetLastInputInfo(ref LastInputInfo info);
}
