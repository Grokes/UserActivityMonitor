using System.Drawing.Imaging;

namespace UserActivityMonitor.Client;

static class ScreenshotCapture
{
    const int MaxImageBytes = 5 * 1024 * 1024;

    public static byte[] CapturePng()
    {
        var screen = Screen.PrimaryScreen
            ?? throw new InvalidOperationException("Primary monitor is unavailable.");
        var bounds = screen.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            throw new InvalidOperationException("Primary monitor has invalid dimensions.");

        using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size,
                CopyPixelOperation.SourceCopy);
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        if (stream.Length > MaxImageBytes)
            throw new InvalidOperationException("Screenshot exceeds the 5 MB upload limit.");

        return stream.ToArray();
    }
}
