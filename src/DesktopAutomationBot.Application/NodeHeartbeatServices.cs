namespace DesktopAutomationBot.Application;

public interface INodeHeartbeatService
{
    Task<RegisteredNode> BeatAsync(
        CancellationToken cancellationToken = default);

    Task RunAsync(
        CancellationToken cancellationToken = default);
}

public sealed class NodeHeartbeatService(
    INodeIdentityProvider identityProvider,
    INodeRegistryStore store,
    BotOptions options,
    TimeProvider timeProvider) : INodeHeartbeatService
{
    public async Task<RegisteredNode> BeatAsync(
        CancellationToken cancellationToken = default)
    {
        var identity = await identityProvider.GetAsync(
            cancellationToken);

        return await store.HeartbeatAsync(
            identity.NodeId,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public async Task RunAsync(
        CancellationToken cancellationToken = default)
    {
        var intervalSeconds =
            options.Node.HeartbeatIntervalSeconds;

        if (intervalSeconds <= 0)
        {
            throw new InvalidOperationException(
                "Node heartbeatIntervalSeconds must be greater than zero.");
        }

        var interval = TimeSpan.FromSeconds(
            intervalSeconds);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(
                    interval,
                    timeProvider,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await BeatAsync(cancellationToken);
        }
    }
}
