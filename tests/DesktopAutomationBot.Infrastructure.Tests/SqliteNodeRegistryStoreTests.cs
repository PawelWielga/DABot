using DesktopAutomationBot.Application;
using DesktopAutomationBot.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace DesktopAutomationBot.Infrastructure.Tests;

public sealed class SqliteNodeRegistryStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(
            Path.GetTempPath(),
            "dabot-node-registry-tests",
            Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RegisterAsync_AcrossStoreInstances_PreservesFirstRegistrationAndRefreshesMetadata()
    {
        var databasePath = Path.Combine(
            _directory,
            "registry.db");
        var options = CreateOptions(databasePath);
        var nodeId = Guid.NewGuid();
        var firstRegisteredAt =
            DateTimeOffset.Parse(
                "2026-10-02T08:00:00+00:00");
        var later =
            firstRegisteredAt.AddHours(2);
        var firstMetadata = CreateMetadata(
            "worker-1",
            "0.1.0",
            executionSlots: 1);
        var refreshedMetadata = CreateMetadata(
            "worker-1-renamed",
            "0.1.1",
            executionSlots: 3);

        var firstStore = new SqliteRunStore(options);
        var first = await firstStore.RegisterAsync(
            nodeId,
            firstRegisteredAt,
            firstMetadata);

        var restartedStore = new SqliteRunStore(options);
        var second = await restartedStore.RegisterAsync(
            nodeId,
            later,
            refreshedMetadata);

        first.RegisteredAt.Should()
            .Be(firstRegisteredAt);
        first.LastSeenAt.Should()
            .Be(firstRegisteredAt);
        second.RegisteredAt.Should()
            .Be(firstRegisteredAt);
        second.LastSeenAt.Should()
            .Be(later);
        second.Metadata.Should()
            .BeEquivalentTo(refreshedMetadata);

        var loaded = await restartedStore.LoadNodeAsync(
            nodeId);
        loaded.Should().NotBeNull();
        loaded!.RegisteredAt.Should()
            .Be(firstRegisteredAt);
        loaded.Metadata.Should()
            .BeEquivalentTo(refreshedMetadata);
    }

    [Fact]
    public async Task ListAsync_ReturnsDurableNodesInRegistrationOrder()
    {
        var databasePath = Path.Combine(
            _directory,
            "registry-list.db");
        var store = new SqliteRunStore(
            CreateOptions(databasePath));
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await store.RegisterAsync(
            secondId,
            now.AddMinutes(1),
            CreateMetadata("second"));
        await store.RegisterAsync(
            firstId,
            now,
            CreateMetadata("first"));

        var nodes = await store.ListNodesAsync();

        nodes.Should().HaveCount(2);
        nodes[0].NodeId.Should().Be(firstId);
        nodes[0].Metadata.DisplayName.Should()
            .Be("first");
        nodes[1].NodeId.Should().Be(secondId);
        nodes[1].Metadata.DisplayName.Should()
            .Be("second");
    }

    [Fact]
    public async Task RegisterAsync_ConcurrentStores_ConvergeOnSingleRegistration()
    {
        var databasePath = Path.Combine(
            _directory,
            "registry-concurrent.db");
        var options = CreateOptions(databasePath);
        var nodeId = Guid.NewGuid();
        var firstTime = DateTimeOffset.Parse(
            "2026-10-02T08:00:00+00:00");
        var secondTime = firstTime.AddMinutes(1);
        var metadata = CreateMetadata("worker-1");

        // Initialize the schema before testing concurrent registration itself.
        await new SqliteRunStore(options).ListNodesAsync();

        var firstStore = new SqliteRunStore(options);
        var secondStore = new SqliteRunStore(options);

        var results = await Task.WhenAll(
            firstStore.RegisterAsync(
                nodeId,
                firstTime,
                metadata),
            secondStore.RegisterAsync(
                nodeId,
                secondTime,
                metadata));

        results[0].RegisteredAt.Should()
            .Be(results[1].RegisteredAt);
        results[0].RegisteredAt.Should()
            .BeOneOf(firstTime, secondTime);
        results[0].Metadata.Should()
            .BeEquivalentTo(metadata);
        results[1].Metadata.Should()
            .BeEquivalentTo(metadata);

        var nodes = await firstStore.ListNodesAsync();
        nodes.Should().ContainSingle();
        nodes[0].NodeId.Should().Be(nodeId);
    }

    [Fact]
    public async Task RegisterAsync_UpgradesVersion6StoreToCurrentVersion()
    {
        Directory.CreateDirectory(_directory);
        var databasePath = Path.Combine(
            _directory,
            "upgrade-v6.db");

        await using (var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
            }.ToString()))
        {
            await connection.OpenAsync();
            await using var command =
                connection.CreateCommand();
            command.CommandText =
                "PRAGMA user_version = 6;";
            await command.ExecuteNonQueryAsync();
        }

        var store = new SqliteRunStore(
            CreateOptions(databasePath));

        await store.RegisterAsync(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            CreateMetadata("worker-1"));

        await using var upgraded = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
            }.ToString());
        await upgraded.OpenAsync();

        await using var version =
            upgraded.CreateCommand();
        version.CommandText = "PRAGMA user_version;";

        Convert.ToInt32(
                await version.ExecuteScalarAsync())
            .Should()
            .Be(9);

        await using var columns =
            upgraded.CreateCommand();
        columns.CommandText =
            """
            SELECT COUNT(*)
            FROM pragma_table_info('Nodes')
            WHERE name IN (
                'DisplayName',
                'OperatingSystem',
                'DABotVersion',
                'BrowserVersionsJson',
                'TagsJson',
                'CapabilitiesJson',
                'ExecutionSlots',
                'LastSeenAt');
            """;

        Convert.ToInt32(
                await columns.ExecuteScalarAsync())
            .Should()
            .Be(8);
    }

    [Fact]
    public async Task RegisterAsync_ExistingVersion7Node_BackfillsAndRefreshesMetadata()
    {
        Directory.CreateDirectory(_directory);
        var databasePath = Path.Combine(
            _directory,
            "upgrade-v7.db");
        var nodeId = Guid.NewGuid();
        var registeredAt = DateTimeOffset.Parse(
            "2026-10-02T07:00:00+00:00");

        await using (var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
            }.ToString()))
        {
            await connection.OpenAsync();
            await using var command =
                connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE Nodes (
                    NodeId TEXT NOT NULL PRIMARY KEY,
                    RegisteredAt TEXT NOT NULL
                );

                INSERT INTO Nodes (
                    NodeId,
                    RegisteredAt)
                VALUES (
                    $nodeId,
                    $registeredAt);

                PRAGMA user_version = 7;
                """;
            command.Parameters.AddWithValue(
                "$nodeId",
                nodeId.ToString("D"));
            command.Parameters.AddWithValue(
                "$registeredAt",
                registeredAt.ToString("O"));
            await command.ExecuteNonQueryAsync();
        }

        var store = new SqliteRunStore(
            CreateOptions(databasePath));

        var migrated = await store.LoadNodeAsync(nodeId);

        migrated.Should().NotBeNull();
        migrated!.RegisteredAt.Should().Be(registeredAt);
        migrated.LastSeenAt.Should().Be(registeredAt);
        migrated.Metadata.DisplayName.Should()
            .Be(nodeId.ToString("D"));
        migrated.Metadata.OperatingSystem.Should()
            .Be("unknown");
        migrated.Metadata.ExecutionSlots.Should().Be(1);

        var currentMetadata = CreateMetadata(
            "worker-upgraded",
            "0.1.1",
            executionSlots: 4);

        var refreshed = await store.RegisterAsync(
            nodeId,
            registeredAt.AddHours(4),
            currentMetadata);

        refreshed.RegisteredAt.Should()
            .Be(registeredAt);
        refreshed.LastSeenAt.Should()
            .Be(registeredAt.AddHours(4));
        refreshed.Metadata.Should()
            .BeEquivalentTo(currentMetadata);
    }


    [Fact]
    public async Task HeartbeatAsync_UpdatesLastSeenWithoutChangingRegistrationOrMetadata()
    {
        var databasePath = Path.Combine(
            _directory,
            "heartbeat.db");
        var store = new SqliteRunStore(
            CreateOptions(databasePath));
        var nodeId = Guid.NewGuid();
        var registeredAt = DateTimeOffset.Parse(
            "2026-10-02T08:00:00+00:00");
        var heartbeatAt = registeredAt.AddMinutes(5);
        var metadata = CreateMetadata("worker-1");

        await store.RegisterAsync(
            nodeId,
            registeredAt,
            metadata);

        var heartbeat = await store.HeartbeatAsync(
            nodeId,
            heartbeatAt);

        heartbeat.RegisteredAt.Should()
            .Be(registeredAt);
        heartbeat.LastSeenAt.Should()
            .Be(heartbeatAt);
        heartbeat.Metadata.Should()
            .BeEquivalentTo(metadata);
    }

    [Fact]
    public async Task HeartbeatAsync_OlderTimestamp_DoesNotMoveLastSeenBackwards()
    {
        var databasePath = Path.Combine(
            _directory,
            "heartbeat-monotonic.db");
        var store = new SqliteRunStore(
            CreateOptions(databasePath));
        var nodeId = Guid.NewGuid();
        var registeredAt = DateTimeOffset.Parse(
            "2026-10-02T08:00:00+00:00");
        var newer = registeredAt.AddMinutes(10);
        var older = registeredAt.AddMinutes(5);

        await store.RegisterAsync(
            nodeId,
            registeredAt,
            CreateMetadata("worker-1"));

        await store.HeartbeatAsync(
            nodeId,
            newer);
        var result = await store.HeartbeatAsync(
            nodeId,
            older);

        result.LastSeenAt.Should().Be(newer);
    }

    [Fact]
    public async Task HeartbeatAsync_UnregisteredNode_Throws()
    {
        var store = new SqliteRunStore(
            CreateOptions(
                Path.Combine(
                    _directory,
                    "heartbeat-missing.db")));

        Func<Task> action = async () =>
            await store.HeartbeatAsync(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow);

        await action.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*is not registered*");
    }

    [Fact]
    public async Task RegisterAsync_EmptyNodeId_Throws()
    {
        var store = new SqliteRunStore(
            CreateOptions(
                Path.Combine(
                    _directory,
                    "invalid.db")));

        Func<Task> action = async () =>
            await store.RegisterAsync(
                Guid.Empty,
                DateTimeOffset.UtcNow,
                CreateMetadata("worker-1"));

        await action.Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("*Node ID must not be empty*");
    }

    private static NodeMetadata CreateMetadata(
        string displayName,
        string version = "0.1.0",
        int executionSlots = 2) =>
        NodeMetadata.Validate(
            new NodeMetadata
            {
                DisplayName = displayName,
                OperatingSystem = "Linux (X64)",
                DABotVersion = version,
                BrowserVersions =
                    new Dictionary<string, string>
                    {
                        ["chromium"] = "140.0",
                    },
                Tags = ["home-lab"],
                Capabilities =
                [
                    "chromium",
                    "interactive",
                    "linux",
                ],
                ExecutionSlots = executionSlots,
            });

    private static BotOptions CreateOptions(
        string databasePath) =>
        new()
        {
            Storage = new StorageOptions
            {
                DatabasePath = databasePath,
            },
        };

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(
                _directory,
                recursive: true);
        }
    }
}
