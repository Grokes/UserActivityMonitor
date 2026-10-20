using System.Security.Principal;

namespace UserActivityMonitor.Client;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var user = WindowsIdentity.GetCurrent();
        using var instance = new Mutex(false, $@"Local\UserActivityMonitor-{user.User?.Value}");
        bool ownsInstance;
        try
        {
            ownsInstance = instance.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            ownsInstance = true;
        }
        if (!ownsInstance)
        {
            MessageBox.Show("Клиент уже запущен. Настройки доступны через иконку в трее.",
                "User Activity Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            var store = new ClientSettingsStore();
            ClientSettings? settings;
            try
            {
                settings = store.Load();
            }
            catch (InvalidDataException exception)
            {
                MessageBox.Show($"{exception.Message}\nУкажите адрес сервера заново.",
                    "User Activity Monitor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                settings = null;
            }

            var firstRun = settings is null;
            if (settings is null)
            {
                using var form = new ServerSettingsForm(store,
                    Environment.GetEnvironmentVariable("USER_ACTIVITY_MONITOR_SERVER_URL"));
                if (form.ShowDialog() != DialogResult.OK) return;
                settings = form.Settings;
            }

            var identity = ClientIdentity.LoadOrCreate();
            var autostart = new AutostartManager();
            try
            {
                // Refresh the path if the enabled client was moved or replaced.
                if (firstRun || autostart.IsEnabled()) autostart.SetEnabled(true);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException
                or System.Security.SecurityException or IOException or InvalidOperationException or ArgumentException)
            {
                MessageBox.Show($"Не удалось настроить автозапуск: {exception.Message}\nКлиент продолжит работу.",
                    "User Activity Monitor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            using var context = new TrayApplicationContext(settings, store, autostart, identity);
            Application.Run(context);
        }
        catch (Exception exception)
        {
            MessageBox.Show($"Не удалось запустить клиент: {exception.Message}",
                "User Activity Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            instance.ReleaseMutex();
        }
    }
}
