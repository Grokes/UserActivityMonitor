namespace UserActivityMonitor.Client;

class ServerSettingsForm : Form
{
    readonly TextBox _serverUrl;
    readonly ClientSettingsStore _store;
    ClientSettings? _settings;

    public ClientSettings Settings => _settings
        ?? throw new InvalidOperationException("Настройки ещё не сохранены.");

    public ServerSettingsForm(ClientSettingsStore store, string? serverUrl)
    {
        _store = store;
        Text = "User Activity Monitor — сервер";
        Icon = SystemIcons.Application;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var layout = new TableLayoutPanel
        {
            AutoSize = true, Padding = new Padding(20), ColumnCount = 1,
            Width = 480
        };
        layout.Controls.Add(new Label { Text = "Адрес сервера", AutoSize = true });
        _serverUrl = new TextBox
        {
            Text = serverUrl ?? "", Width = 420,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(3, 10, 3, 10)
        };
        layout.Controls.Add(_serverUrl);
        layout.Controls.Add(new Label
        {
            Text = "Например: http://10.0.2.2:5031\nПосле сохранения приложение продолжит работу в трее.",
            AutoSize = true, MaximumSize = new Size(420, 0), Margin = new Padding(3, 0, 3, 16)
        });
        var save = new Button { Text = "Сохранить", AutoSize = true };
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, AutoSize = true };
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        layout.Controls.Add(buttons);
        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;
        save.Click += (_, _) => Save();
        Shown += (_, _) => { _serverUrl.Focus(); _serverUrl.SelectAll(); };
    }

    void Save()
    {
        if (!ClientSettingsStore.TryNormalizeUrl(_serverUrl.Text, out var url))
        {
            MessageBox.Show(this, "Укажите http://host:port или https://host:port без пути, логина и параметров.",
                "Адрес сервера", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _serverUrl.Focus();
            return;
        }

        try
        {
            var settings = new ClientSettings(url);
            _store.Save(settings);
            _settings = settings;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Не удалось сохранить настройки: {exception.Message}",
                "Настройки", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
