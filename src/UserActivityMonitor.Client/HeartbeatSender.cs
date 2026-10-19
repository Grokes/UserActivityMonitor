using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using UserActivityMonitor.Contracts;

namespace UserActivityMonitor.Client;

class HeartbeatSender(
    HttpClient httpClient, ClientIdentity identity, Func<byte[]>? captureScreenshot = null)
{
    readonly Func<byte[]> _captureScreenshot = captureScreenshot ?? ScreenshotCapture.CapturePng;
    PendingScreenshot? _pending;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        try
        {
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                var heartbeat = new HeartbeatRequest(
                    identity.ClientId,
                    identity.DomainName,
                    identity.MachineName,
                    identity.UserName,
                    InputReader.ReadUtc());

                try
                {
                    using var response = await httpClient.PostAsJsonAsync(
                        "/client/heartbeat", heartbeat, cancellationToken);
                    response.EnsureSuccessStatusCode();
                    if (response.StatusCode != HttpStatusCode.NoContent)
                    {
                        var reply = await response.Content.ReadFromJsonAsync<HeartbeatResponse>(cancellationToken);
                        await ProcessScreenshotAsync(reply?.Screenshot, cancellationToken);
                    }
                }
                catch (HttpRequestException exception)
                {
                    Trace.TraceWarning($"Agent request failed: {exception.Message}");
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    Trace.TraceWarning("Agent request timed out.");
                }
                catch (JsonException exception)
                {
                    Trace.TraceWarning($"Invalid heartbeat response: {exception.Message}");
                }
            }
            while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown, including cancellation of an in-flight request.
        }
    }

    async Task ProcessScreenshotAsync(ScreenshotCommand? command, CancellationToken cancellationToken)
    {
        if (command is null || command.RequestId == Guid.Empty || command.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _pending = null;
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (_pending?.RequestId != command.RequestId)
        {
            try
            {
                _pending = new PendingScreenshot(command.RequestId, _captureScreenshot(), null);
            }
            catch (Exception exception) when (exception is ExternalException
                or InvalidOperationException or ArgumentException or NotSupportedException)
            {
                var message = $"Unable to capture desktop: {exception.Message}";
                _pending = new PendingScreenshot(command.RequestId, null, message[..Math.Min(message.Length, 1000)]);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (command.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _pending = null;
            return;
        }

        var path = $"/client/{identity.ClientId}/screenshots/{command.RequestId}";
        HttpResponseMessage response;
        if (_pending.Error is { } error)
        {
            response = await httpClient.PostAsJsonAsync(
                path + "/error", new ScreenshotErrorRequest(error), cancellationToken);
        }
        else
        {
            using var content = new ByteArrayContent(_pending.Image!);
            content.Headers.ContentType = new("image/png");
            response = await httpClient.PostAsync(path, content, cancellationToken);
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                Trace.TraceInformation($"Screenshot request {command.RequestId} is no longer pending.");
                _pending = null;
                return;
            }

            // Keep the captured result on network/server errors for the next heartbeat.
            response.EnsureSuccessStatusCode();
            _pending = null;
        }
    }
}
