using System.Text.Json;

namespace UserActivityMonitor.Client;

class ClientSettingsStore(string? directory = null)
{
    readonly string _directory = directory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UserActivityMonitor");

    public ClientSettings? Load()
    {
        var path = Path.Combine(_directory, "settings.json");
        if (!File.Exists(path)) return null;

        try
        {
            var settings = JsonSerializer.Deserialize<ClientSettings>(File.ReadAllText(path));
            if (settings is null || !TryNormalizeUrl(settings.ServerUrl, out var url))
                throw new InvalidDataException("Некорректный адрес сервера в настройках.");
            return new ClientSettings(url);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Файл настроек повреждён.", exception);
        }
    }

    public void Save(ClientSettings settings)
    {
        if (!TryNormalizeUrl(settings.ServerUrl, out var url))
            throw new ArgumentException("Укажите адрес сервера в формате http://host:port или https://host:port.");

        Directory.CreateDirectory(_directory);
        var temporary = Path.Combine(_directory, $"settings-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new ClientSettings(url)));
            File.Move(temporary, Path.Combine(_directory, "settings.json"), overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static bool TryNormalizeUrl(string? value, out string url)
    {
        url = "";
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(uri.Host) || uri.UserInfo.Length > 0
            || uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            return false;

        url = uri.GetLeftPart(UriPartial.Authority);
        return true;
    }
}
