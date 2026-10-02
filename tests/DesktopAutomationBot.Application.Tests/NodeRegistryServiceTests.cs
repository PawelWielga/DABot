using DesktopAutomationBot.Application;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class NodeRegistryServiceTests
{
    [Fact]
    public async Task EnsureLocalNodeRegisteredAsync_UsesIdentityMetadataAndCurrentTime()
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
        var metadata = CreateMetadata();
        var store = new RecordingNodeRegistryStore();
        var service = new NodeRegistryService(
            new RecordingIdentityProvider(nodeId),
            new RecordingMetadataProvider(metadata),
            store,
            new FixedTimeProvider(now));

        var result =
            await service.EnsureLocalNodeRegisteredAsync();

        result.NodeId.Should().Be(nodeId);
        result.RegisteredAt.Should().Be(now);
        result.LastSeenAt.Should().Be(now);
        result.Metadata.Should().BeEquivalentTo(metadata);

        store.RegisteredNodeId.Should().Be(nodeId);
        store.RegisteredAt.Should().Be(now);
        store.RegisteredMetadata.Should()
            .BeEquivalentTo(metadata);
    }

    [Fact]
    public async Task GetAsync_EmptyNodeId_ThrowsBeforeStoreCall()
    {
        var store = new RecordingNodeRegistryStore();
        var service = new NodeRegistryService(
            new RecordingIdentityProvider(Guid.NewGuid()),
            new RecordingMetadataProvider(CreateMetadata()),
            store,
            TimeProvider.System);

        Func<Task> action = async () =>
            await service.GetAsync(Guid.Empty);

        await action.Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("*Node ID must not be empty*");

        store.LoadCalls.Should().Be(0);
    }

    private static NodeMetadata CreateMetadata() =>
        NodeMetadata.Validate(
            new NodeMetadata
            {
                DisplayName = "worker-1",
                OperatingSystem = "Linux (X64)",
                DABotVersion = "0.1.0",
                BrowserVersions =
                    new Dictionary<string, string>
                    {
                        ["chromium"] = "140.0",
                    },
                Tags = ["home-lab"],
                Capabilities = ["linux", "chromium"],
                ExecutionSlots = 2,
            });

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

    private sealed class RecordingMetadataProvider(
        NodeMetadata metadata) : INodeMetadataProvider
    {
        public Task<NodeMetadata> GetAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(metadata);
        }
    }

    private sealed class RecordingNodeRegistryStore :
        INodeRegistryStore
    {
        public Guid? RegisteredNodeId { get; private set; }

        public DateTimeOffset? RegisteredAt { get; private set; }

        public NodeMetadata? RegisteredMetadata { get; private set; }

        public int LoadCalls { get; private set; }

        public Task<RegisteredNode> RegisterAsync(
            Guid nodeId,
            DateTimeOffset registeredAt,
            NodeMetadata metadata,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RegisteredNodeId = nodeId;
            RegisteredAt = registeredAt;
            RegisteredMetadata = metadata;

            return Task.FromResult(
                new RegisteredNode
                {
                    NodeId = nodeId,
                    RegisteredAt = registeredAt,
                    LastSeenAt = registeredAt,
                    Metadata = metadata,
                });
        }

        public Task<RegisteredNode> HeartbeatAsync(
            Guid nodeId,
            DateTimeOffset lastSeenAt,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

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
