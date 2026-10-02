using DesktopAutomationBot.Application;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class NodeRegistryServiceTests
{
    [Fact]
    public async Task EnsureLocalNodeRegisteredAsync_UsesStableIdentityAndCurrentTime()
    {
        var nodeId = Guid.Parse(
            "a1000000-0000-4000-8000-000000000001");
        var now = new DateTimeOffset(
            2026,
            10,
            2,
            10,
            30,
            0,
            TimeSpan.Zero);
        var identity = new RecordingIdentityProvider(nodeId);
        var store = new RecordingNodeRegistryStore();
        var service = new NodeRegistryService(
            identity,
            store,
            new FixedTimeProvider(now));

        var result =
            await service.EnsureLocalNodeRegisteredAsync();

        result.NodeId.Should().Be(nodeId);
        result.RegisteredAt.Should().Be(now);
        store.RegisteredNodeId.Should().Be(nodeId);
        store.RegisteredAt.Should().Be(now);
    }

    [Fact]
    public async Task GetAsync_EmptyNodeId_ThrowsBeforeStoreCall()
    {
        var store = new RecordingNodeRegistryStore();
        var service = new NodeRegistryService(
            new RecordingIdentityProvider(Guid.NewGuid()),
            store,
            TimeProvider.System);

        Func<Task> action = async () =>
            await service.GetAsync(Guid.Empty);

        await action.Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("*Node ID must not be empty*");

        store.LoadCalls.Should().Be(0);
    }

    private sealed class RecordingIdentityProvider(
        Guid nodeId) : INodeIdentityProvider
    {
        public Task<NodeIdentity> GetAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                new NodeIdentity
                {
                    NodeId = nodeId,
                });
        }
    }

    private sealed class RecordingNodeRegistryStore :
        INodeRegistryStore
    {
        public Guid? RegisteredNodeId { get; private set; }

        public DateTimeOffset? RegisteredAt { get; private set; }

        public int LoadCalls { get; private set; }

        public Task<RegisteredNode> RegisterAsync(
            Guid nodeId,
            DateTimeOffset registeredAt,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RegisteredNodeId = nodeId;
            RegisteredAt = registeredAt;

            return Task.FromResult(
                new RegisteredNode
                {
                    NodeId = nodeId,
                    RegisteredAt = registeredAt,
                });
        }

        public Task<RegisteredNode?> LoadNodeAsync(
            Guid nodeId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadCalls++;
            return Task.FromResult<RegisteredNode?>(null);
        }

        public Task<IReadOnlyList<RegisteredNode>> ListNodesAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<RegisteredNode>>([]);
        }
    }

    private sealed class FixedTimeProvider(
        DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
