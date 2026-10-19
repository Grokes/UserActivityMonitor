using UserActivityMonitor.Contracts;

namespace UserActivityMonitor.Server;

static class ScreenshotEndpoints
{
    public static void MapScreenshotEndpoints(this WebApplication app)
    {
        app.MapPost("/clients/{clientId:guid}/screenshots",
            (Guid clientId, ClientRegistry clients, ScreenshotRegistry screenshots) =>
            {
                var client = clients.GetClients().FirstOrDefault(client => client.ClientId == clientId);
                if (client is null) return Results.NotFound();
                if (!client.IsOnline) return Results.Conflict(new { error = "Client is offline." });
                var info = screenshots.Create(clientId);
                return info is null
                    ? Results.Problem(statusCode: 503, title: "Screenshot request limit reached.")
                    : Results.Accepted($"/screenshots/{info.RequestId}", info);
            });

        app.MapGet("/screenshots/{requestId:guid}",
            (Guid requestId, ScreenshotRegistry screenshots) =>
                screenshots.Get(requestId) is { } info ? Results.Ok(info) : Results.NotFound());

        app.MapGet("/screenshots/{requestId:guid}/image",
            (Guid requestId, ScreenshotRegistry screenshots, HttpContext context) =>
            {
                context.Response.Headers.CacheControl = "no-store";
                if (screenshots.Get(requestId) is not { } info) return Results.NotFound();
                if (info.State != ScreenshotState.Completed)
                    return Results.Conflict(new { error = "Screenshot is not available.", info.State });
                var image = screenshots.GetImage(requestId);
                return image is null ? Results.NotFound() : Results.File(image, "image/png");
            });

        app.MapPost("/client/{clientId:guid}/screenshots/{requestId:guid}", UploadImage);
        app.MapPost("/client/{clientId:guid}/screenshots/{requestId:guid}/error",
            (Guid clientId, Guid requestId, ScreenshotErrorRequest error, ScreenshotRegistry screenshots) =>
            {
                if (string.IsNullOrWhiteSpace(error.Message) || error.Message.Length > 1000)
                    return Results.BadRequest(new { error = "Message must contain 1 to 1000 characters." });
                return ToResult(screenshots.Fail(clientId, requestId, error.Message));
            });
    }

    static async Task<IResult> UploadImage(
        Guid clientId, Guid requestId, HttpRequest request, ScreenshotRegistry screenshots)
    {
        if (screenshots.Get(requestId) is not { } info) return Results.NotFound();
        if (info.ClientId != clientId) return Results.StatusCode(403);
        if (info.State == ScreenshotState.Expired) return Results.StatusCode(410);
        if (info.State != ScreenshotState.Pending) return Results.Conflict();
        if (!string.Equals(request.ContentType, "image/png", StringComparison.OrdinalIgnoreCase))
            return Results.StatusCode(415);
        if (request.ContentLength > ScreenshotRegistry.MaxImageBytes) return Results.StatusCode(413);

        using var image = new MemoryStream();
        var buffer = new byte[64 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(buffer, request.HttpContext.RequestAborted)) > 0)
        {
            if (image.Length + read > ScreenshotRegistry.MaxImageBytes) return Results.StatusCode(413);
            image.Write(buffer, 0, read);
        }

        var bytes = image.ToArray();
        // Check the PNG signature without a platform-specific image library.
        byte[] signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (bytes.Length <= signature.Length || !bytes.AsSpan(0, signature.Length).SequenceEqual(signature))
            return Results.BadRequest(new { error = "Expected a PNG image." });

        return ToResult(screenshots.Complete(clientId, requestId, bytes));
    }

    static IResult ToResult(ScreenshotUpdateResult result) => result switch
    {
        ScreenshotUpdateResult.Stored => Results.NoContent(),
        ScreenshotUpdateResult.NotFound => Results.NotFound(),
        ScreenshotUpdateResult.WrongClient => Results.StatusCode(403),
        ScreenshotUpdateResult.Expired => Results.StatusCode(410),
        _ => Results.Conflict(new { error = "Request has already been completed." })
    };
}
