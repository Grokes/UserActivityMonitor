using System.Diagnostics;
using System.Net.Http.Json;
using UserActivityMonitor.Contracts;

namespace UserActivityMonitor.Client;

public class HeartbeatSender(HttpClient httpClient, ClientIdentity identity)
{
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
                }
                catch (HttpRequestException exception)
                {
                    Trace.TraceWarning($"Heartbeat failed: {exception.Message}");
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    Trace.TraceWarning("Heartbeat request timed out.");
                }
            } while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown, including cancellation of an in-flight request.
        }
    }
}