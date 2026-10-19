using UserActivityMonitor.Contracts;

namespace UserActivityMonitor.Server;

public static class ClientEndopoints
{
    public static void MapClientEndpoints(this WebApplication app)
    {
        app.MapPost("/client/heartbeat", RecordHeartbeat);
        app.MapGet("/clients", (ClientRegistry registry) => Results.Ok(registry.GetClients()));
    }

    static IResult RecordHeartbeat(
        HeartbeatRequest heartbeat,
        HttpContext context,
        ClientRegistry registry,
        ScreenshotRegistry screenshots)
    {
        var errors = new Dictionary<string, string[]>();

        if (heartbeat.ClientId == Guid.Empty)
            errors[nameof(heartbeat.ClientId)] = ["ClientId must not be empty."];

        // An empty domain is allowed for computers without a domain.
        if (heartbeat.DomainName is null || heartbeat.DomainName.Length > 255)
            errors[nameof(heartbeat.DomainName)] = ["DomainName must be a string of at most 255 characters."];

        if (string.IsNullOrWhiteSpace(heartbeat.MachineName) || heartbeat.MachineName.Length > 255)
            errors[nameof(heartbeat.MachineName)] = ["MachineName is required and must not exceed 255 characters."];

        if (string.IsNullOrWhiteSpace(heartbeat.UserName) || heartbeat.UserName.Length > 255)
            errors[nameof(heartbeat.UserName)] = ["UserName is required and must not exceed 255 characters."];

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var address = context.Connection.RemoteIpAddress;
        if (address?.IsIPv4MappedToIPv6 == true)
            address = address.MapToIPv4();

        registry.RecordHeartbeat(heartbeat, address?.ToString());
        return Results.Ok(new HeartbeatResponse(screenshots.GetPendingCommand(heartbeat.ClientId)));
    }
}
