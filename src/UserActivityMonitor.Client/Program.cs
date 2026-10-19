namespace UserActivityMonitor.Client;

public class Program
{
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Keep the agent running in the user session without opening a window.
        using var context = new ApplicationContext();
        Application.Run(context);
    }
}