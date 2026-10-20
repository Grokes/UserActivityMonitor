using Microsoft.Win32;

namespace UserActivityMonitor.Client;

class AutostartManager
{
    const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "UserActivityMonitor";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(ValueName) is string command && !string.IsNullOrWhiteSpace(command);
    }

    public void SetEnabled(bool enabled)
    {
        if (!enabled)
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var path = Environment.ProcessPath
            ?? throw new InvalidOperationException("Не удалось определить путь приложения.");
        var command = BuildCommand(path);
        using var startup = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
        startup.SetValue(ValueName, command, RegistryValueKind.String);
    }

    public static string BuildCommand(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || executablePath.Contains('"'))
            throw new ArgumentException("Некорректный путь приложения.", nameof(executablePath));
        var command = $"\"{executablePath}\"";
        if (command.Length > 260)
            throw new InvalidOperationException("Путь слишком длинный для автозапуска. Переместите EXE в папку с более коротким путём.");
        return command;
    }
}
