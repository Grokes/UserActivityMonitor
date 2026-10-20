using System.Diagnostics;
using Microsoft.Win32;

namespace UserActivityMonitor.Client;

class TrayApplicationContext : ApplicationContext
{
    readonly ClientSettingsStore _store;
    readonly AutostartManager _autostart;
    readonly ClientIdentity _identity;
    readonly NotifyIcon _tray;
    readonly ContextMenuStrip _menu;
    readonly ToolStripMenuItem _settingsMenu;
    readonly ToolStripMenuItem _autostartMenu;
    readonly WindowsFormsSynchronizationContext _uiContext = new();
    ClientSettings _settings;
    ServerSettingsForm? _settingsForm;
    CancellationTokenSource? _cancellation;
    HttpClient? _httpClient;
    Task _sending = Task.CompletedTask;
    Task _stopping = Task.CompletedTask;
    bool _editing;
    bool _exiting;
    bool _disposed;

    public TrayApplicationContext(ClientSettings settings, ClientSettingsStore store,
        AutostartManager autostart, ClientIdentity identity)
    {
        _settings = settings;
        _store = store;
        _autostart = autostart;
        _identity = identity;
        _settingsMenu = new ToolStripMenuItem("Настройки сервера…");
        _autostartMenu = new ToolStripMenuItem("Автозапуск при входе");
        var exit = new ToolStripMenuItem("Выход");
        _menu = new ContextMenuStrip();
        _menu.Items.AddRange([_settingsMenu, _autostartMenu, new ToolStripSeparator(), exit]);
        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "User Activity Monitor",
            ContextMenuStrip = _menu,
            Visible = true
        };

        _settingsMenu.Click += async (_, _) => await EditSettingsAsync();
        _tray.DoubleClick += async (_, _) => await EditSettingsAsync();
        _autostartMenu.Click += (_, _) => ToggleAutostart();
        exit.Click += async (_, _) => await ExitAsync();
        _menu.Opening += (_, _) => RefreshAutostart();
        SystemEvents.SessionEnded += OnSessionEnded;
        RefreshAutostart();
        StartSender();
    }

    void StartSender()
    {
        _cancellation = new CancellationTokenSource();
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_settings.ServerUrl), Timeout = TimeSpan.FromSeconds(3)
        };
        var client = _httpClient;
        var token = _cancellation.Token;
        _sending = Task.Run(async () =>
        {
            try
            {
                await new HeartbeatSender(client, _identity).RunAsync(token);
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
                if (!token.IsCancellationRequested)
                    _uiContext.Post(_ =>
                    {
                        if (!_exiting)
                            _tray.ShowBalloonTip(5000, "User Activity Monitor",
                                "Работа клиента остановлена из-за ошибки. Перезапустите приложение.", ToolTipIcon.Error);
                    }, null);
            }
        });
    }

    Task StopSenderAsync()
    {
        if (_cancellation is null) return _stopping;
        var cancellation = _cancellation;
        var client = _httpClient;
        var sending = _sending;
        _cancellation = null;
        _httpClient = null;
        cancellation.Cancel();
        _stopping = DisposeSenderAsync(sending, client, cancellation);
        return _stopping;
    }

    static async Task DisposeSenderAsync(Task sending, HttpClient? client, CancellationTokenSource cancellation)
    {
        try
        {
            await sending.ConfigureAwait(false);
        }
        finally
        {
            client?.Dispose();
            cancellation.Dispose();
        }
    }

    async Task EditSettingsAsync()
    {
        if (_editing || _exiting) return;
        _editing = true;
        _settingsMenu.Enabled = false;
        try
        {
            using var form = new ServerSettingsForm(_store, _settings.ServerUrl);
            _settingsForm = form;
            var result = form.ShowDialog();
            _settingsForm = null;
            if (result != DialogResult.OK || _exiting) return;
            await StopSenderAsync();
            _settings = form.Settings;
            if (!_exiting) StartSender();
        }
        finally
        {
            _settingsForm = null;
            _editing = false;
            if (!_exiting) _settingsMenu.Enabled = true;
        }
    }

    void RefreshAutostart()
    {
        try
        {
            _autostartMenu.Checked = _autostart.IsEnabled();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            Trace.TraceWarning(exception.Message);
            _autostartMenu.Checked = false;
        }
    }

    void ToggleAutostart()
    {
        try
        {
            _autostart.SetEnabled(!_autostart.IsEnabled());
            RefreshAutostart();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException
            or System.Security.SecurityException or IOException or InvalidOperationException or ArgumentException)
        {
            MessageBox.Show($"Не удалось изменить автозапуск: {exception.Message}",
                "User Activity Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void OnSessionEnded(object sender, SessionEndedEventArgs args) =>
        _uiContext.Post(async _ => await ExitAsync(), null);

    async Task ExitAsync()
    {
        if (_exiting) return;
        _exiting = true;
        _tray.Visible = false;
        _settingsForm?.Close();
        _menu.Enabled = false;
        await StopSenderAsync();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _exiting = true;
            SystemEvents.SessionEnded -= OnSessionEnded;
            StopSenderAsync().GetAwaiter().GetResult();
            _tray.Visible = false;
            _tray.Dispose();
            _menu.Dispose();
            _uiContext.Dispose();
        }
        base.Dispose(disposing);
    }
}
