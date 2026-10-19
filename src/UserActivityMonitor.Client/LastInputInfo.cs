using System.Runtime.InteropServices;

namespace UserActivityMonitor.Client;

[StructLayout(LayoutKind.Sequential)]
struct LastInputInfo
{
    public uint Size;
    public uint Time;
}
