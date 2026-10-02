using DesktopAutomationBot.Application;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class NodeHeartbeatServiceTests
{
    [Fact]
    public async Task BeatAsync_UsesStableIdentityAndCurrentTime()
    {
        var nodeId = Guid.Parse(
            "b1000000-0000-4000-8000-000000000001");
        var now = new DateTimeOffset(
            2026,
            10,
            2,
            11,
            0,
            0,
            TimeSpan.Zero);
        var store = new RecordingStore();
        var service = new NodeHeartbeatService(
            new RecordingIdentityProvider(nodeId),
            store,
            new BotOptions(),
            new FixedTimeProvider(now));

        var result = await service.BeatAsync();

        store.HeartbeatNodeId.Should().Be(nodeId);
        store.HeartbeatAt.Should().Be(now);
        result.NodeId.Should().Be(nodeId);
        result.LastSeenAt.Should().Be(now);
    }

    [Fact]
    public async Task RunAsync_InvalidInterval_ThrowsBeforeWaiting()
    {
        var service = new NodeHeartbeatService(
            new RecordingIdentityProvider(Guid.NewGuid()),
            new RecordingStore(),
            new BotOptions
            {
                Node = new NodeOptions
                {
                    HeartbeatIntervalSeconds = 0,
                },
            },
            TimeProvider.System);

        Func<Task> action = async () =>
            await service.RunAsync();

        await action.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*heartbeatIntervalSeconds must be greater than zero*");
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

    private sealed class RecordingStore : INodeRegistryStore
    {
        public Guid? HeartbeatNodeId { get; private set; }

        public DateTimeOffset? HeartbeatAt { get; private set; }

        public Task<RegisteredNode> RegisterAsync(
            Guid nodeId,
            DateTimeOffset registeredAt,
            NodeMetadata metadata,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RegisteredNode> HeartbeatAsync(
            Guid nodeId,
            DateTimeOffset lastSeenAt,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HeartbeatNodeId = nodeId;
            HeartbeatAt = lastSeenAt;

            return Task.FromResult(
                new RegisteredNode
                {
                    NodeId = nodeId,
                    RegisteredAt = lastSeenAt.AddHours(-1),
                    LastSeenAt = lastSeenAt,
                    Metadata = NodeMetadata.Validate(
                        new NodeMetadata
                        {
                            DisplayName = "worker-1",
                            OperatingSystem = "Linux",
                            DABotVersion = "0.1.0",
                            ExecutionSlots = 1,
                        }),
                });
        }

        public Task<RegisteredNode?> LoadNodeAsync(
            Guid nodeId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<RegisteredNode?>(null);

        public Task<IReadOnlyList<RegisteredNode>> ListNodesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RegisteredNode>>([]);
    }

    private sealed class FixedTimeProvider(
        DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
