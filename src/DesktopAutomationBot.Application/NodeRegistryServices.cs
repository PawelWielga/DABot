namespace DesktopAutomationBot.Application;

public sealed record RegisteredNode
{
    public required Guid NodeId { get; init; }

    public required DateTimeOffset RegisteredAt { get; init; }
}

public interface INodeRegistryStore
{
    Task<RegisteredNode> RegisterAsync(
        Guid nodeId,
        DateTimeOffset registeredAt,
        CancellationToken cancellationToken = default);

    Task<RegisteredNode?> LoadNodeAsync(
        Guid nodeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RegisteredNode>> ListNodesAsync(
        CancellationToken cancellationToken = default);
}

public interface INodeRegistryService
{
    Task<RegisteredNode> EnsureLocalNodeRegisteredAsync(
        CancellationToken cancellationToken = default);

    Task<RegisteredNode?> GetAsync(
        Guid nodeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RegisteredNode>> ListAsync(
        CancellationToken cancellationToken = default);
}

public sealed class NodeRegistryService(
    INodeIdentityProvider identityProvider,
    INodeRegistryStore store,
    TimeProvider timeProvider) : INodeRegistryService
{
    public async Task<RegisteredNode> EnsureLocalNodeRegisteredAsync(
        CancellationToken cancellationToken = default)
    {
        var identity = await identityProvider.GetAsync(
            cancellationToken);

        return await store.RegisterAsync(
            identity.NodeId,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public Task<RegisteredNode?> GetAsync(
        Guid nodeId,
        CancellationToken cancellationToken = default)
    {
        if (nodeId == Guid.Empty)
        {
            throw new ArgumentException(
                "Node ID must not be empty.",
                nameof(nodeId));
        }

        return store.LoadNodeAsync(
            nodeId,
            cancellationToken);
    }

    public Task<IReadOnlyList<RegisteredNode>> ListAsync(
        CancellationToken cancellationToken = default) =>
        store.ListNodesAsync(cancellationToken);
}
