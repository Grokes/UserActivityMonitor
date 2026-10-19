using Microsoft.Win32;

namespace UserActivityMonitor.Client;

public class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var serverUrl = Environment.GetEnvironmentVariable("USER_ACTIVITY_MONITOR_SERVER_URL")
                        ?? "http://localhost:5031";
        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var serverUri)
            || (serverUri.Scheme != Uri.UriSchemeHttp && serverUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "USER_ACTIVITY_MONITOR_SERVER_URL must be an absolute HTTP or HTTPS URL.");
        }

        var identity = ClientIdentity.LoadOrCreate();
        using var httpClient = new HttpClient
        {
            BaseAddress = serverUri,
            Timeout = TimeSpan.FromSeconds(3)
        };
        using var cancellation = new CancellationTokenSource();
        using var context = new ApplicationContext();
        using var uiContext = new WindowsFormsSynchronizationContext();

        // Session events can arrive on a worker thread; exit on the UI thread.
        SessionEndedEventHandler onSessionEnded = (_, _) =>
            uiContext.Post(_ => context.ExitThread(), null);
        SystemEvents.SessionEnded += onSessionEnded;

        var sender = new HeartbeatSender(httpClient, identity);
        var sending = Task.Run(() => sender.RunAsync(cancellation.Token));

        try
        {
            // Run a message loop without a visible window.
            Application.Run(context);
        }
        finally
        {
            SystemEvents.SessionEnded -= onSessionEnded;
            cancellation.Cancel();
            sending.GetAwaiter().GetResult();
        }
    }
}