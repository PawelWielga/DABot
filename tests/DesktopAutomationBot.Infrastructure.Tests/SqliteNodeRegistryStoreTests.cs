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
    public async Task RegisterAsync_AcrossStoreInstances_PreservesFirstRegistration()
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

        var firstStore = new SqliteRunStore(options);
        var first = await firstStore.RegisterAsync(
            nodeId,
            firstRegisteredAt);

        var restartedStore = new SqliteRunStore(options);
        var second = await restartedStore.RegisterAsync(
            nodeId,
            later);

        first.Should().Be(second);
        second.RegisteredAt.Should()
            .Be(firstRegisteredAt);

        var loaded = await restartedStore.LoadAsync(
            nodeId);
        loaded.Should().Be(second);
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
            now.AddMinutes(1));
        await store.RegisterAsync(
            firstId,
            now);

        var nodes = await store.ListAsync();

        nodes.Should().HaveCount(2);
        nodes[0].NodeId.Should().Be(firstId);
        nodes[1].NodeId.Should().Be(secondId);
    }

    [Fact]
    public async Task RegisterAsync_UpgradesVersion6StoreToVersion7()
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
            DateTimeOffset.UtcNow);

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
            .Be(7);

        await using var table =
            upgraded.CreateCommand();
        table.CommandText =
            """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name = 'Nodes';
            """;

        Convert.ToInt32(
                await table.ExecuteScalarAsync())
            .Should()
            .Be(1);
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
                DateTimeOffset.UtcNow);

        await action.Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("*Node ID must not be empty*");
    }

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
