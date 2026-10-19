namespace UserActivityMonitor.Client;

public record ClientIdentity(Guid ClientId, string DomainName, string MachineName, string UserName)
{
    public static ClientIdentity LoadOrCreate()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UserActivityMonitor");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, "client-id");
        using var file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        Guid clientId;

        if (file.Length > 0)
        {
            using var reader = new StreamReader(file, leaveOpen: true);
            if (!Guid.TryParse(reader.ReadToEnd().Trim(), out clientId) || clientId == Guid.Empty)
                throw new InvalidDataException($"Invalid client identifier in {path}.");
        }
        else
        {
            clientId = Guid.NewGuid();
            using var writer = new StreamWriter(file, leaveOpen: true);
            writer.Write(clientId.ToString("D"));
            writer.Flush();
            file.Flush(flushToDisk: true);
        }

        return new ClientIdentity(
            clientId, Environment.UserDomainName, Environment.MachineName, Environment.UserName);
    }
}