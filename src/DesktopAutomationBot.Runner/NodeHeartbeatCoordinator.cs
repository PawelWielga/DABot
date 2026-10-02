using DesktopAutomationBot.Application;

namespace DesktopAutomationBot.Runner;

public static class NodeHeartbeatCoordinator
{
    public static async Task RunAsync(
        Func<CancellationToken, Task> worker,
        INodeHeartbeatService heartbeat,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentNullException.ThrowIfNull(heartbeat);

        using var linkedCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        var workerTask =
            worker(linkedCancellation.Token);
        var heartbeatTask =
            heartbeat.RunAsync(
                linkedCancellation.Token);

        await Task.WhenAny(
            workerTask,
            heartbeatTask);

        await linkedCancellation.CancelAsync();

        try
        {
            await Task.WhenAll(
                workerTask,
                heartbeatTask);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Normal process shutdown.
        }
    }
}
