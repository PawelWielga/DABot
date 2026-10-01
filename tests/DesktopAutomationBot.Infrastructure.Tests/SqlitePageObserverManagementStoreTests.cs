using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using DesktopAutomationBot.Infrastructure;
using FluentAssertions;

namespace DesktopAutomationBot.Infrastructure.Tests;

public sealed class SqlitePageObserverManagementStoreTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(
            Path.GetTempPath(),
            "dabot-observer-management-tests",
            Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task GetDefinitionAsync_ReturnsPersistedDefinition()
    {
        var store = new SqliteRunStore(CreateOptions());
        var definition = CreateDefinition();

        await store.SaveAsync(definition);

        var loaded = await store.GetDefinitionAsync(definition.ObserverId);

        loaded.Should().Be(definition);
    }

    [Fact]
    public async Task SaveDefinitionAsync_WhenResetRequested_ClearsSnapshot()
    {
        var store = new SqliteRunStore(CreateOptions());
        var definition = CreateDefinition();
        var checkedAt = DateTimeOffset.Parse(
            "2026-10-01T18:00:00+00:00");

        await store.SaveAsync(definition);
        await store.SaveSnapshotAsync(
            new PageObserverSnapshot
            {
                ObserverId = definition.ObserverId,
                LastObservation = "Available",
                LastMatched = true,
                LastCheckedAt = checkedAt,
                NextCheckAt = checkedAt.AddSeconds(-1),
                LastEventAt = checkedAt,
                FailureCount = 3,
                LastError = "Old error",
            });

        await store.SaveDefinitionAsync(
            definition with
            {
                Url = "https://example.test/other",
            },
            resetSnapshot: true);

        var due = await store.LoadDueAsync(
            checkedAt.AddMinutes(1));

        due.Should().ContainSingle();
        var stored = due[0];
        stored.Definition.Url.Should().Be("https://example.test/other");
        stored.Snapshot.LastObservation.Should().BeNull();
        stored.Snapshot.LastMatched.Should().BeNull();
        stored.Snapshot.LastCheckedAt.Should().BeNull();
        stored.Snapshot.NextCheckAt.Should().BeNull();
        stored.Snapshot.LastEventAt.Should().BeNull();
        stored.Snapshot.FailureCount.Should().Be(0);
        stored.Snapshot.LastError.Should().BeNull();
    }

    [Fact]
    public async Task SaveDefinitionAsync_WhenResetNotRequested_PreservesSnapshot()
    {
        var store = new SqliteRunStore(CreateOptions());
        var definition = CreateDefinition();
        var checkedAt = DateTimeOffset.Parse(
            "2026-10-01T18:00:00+00:00");

        await store.SaveAsync(definition);
        await store.SaveSnapshotAsync(
            new PageObserverSnapshot
            {
                ObserverId = definition.ObserverId,
                LastObservation = "Available",
                LastMatched = true,
                LastCheckedAt = checkedAt,
                NextCheckAt = checkedAt.AddSeconds(-1),
                LastEventAt = checkedAt,
                FailureCount = 1,
                LastError = "Transient",
            });

        await store.SaveDefinitionAsync(
            definition with
            {
                Name = "Renamed",
                PollIntervalMs = 30000,
            },
            resetSnapshot: false);

        var due = await store.LoadDueAsync(
            checkedAt.AddMinutes(1));

        due.Should().ContainSingle();
        var stored = due[0];
        stored.Definition.Name.Should().Be("Renamed");
        stored.Definition.PollIntervalMs.Should().Be(30000);
        stored.Snapshot.LastObservation.Should().Be("Available");
        stored.Snapshot.LastMatched.Should().BeTrue();
        stored.Snapshot.LastCheckedAt.Should().Be(checkedAt);
        stored.Snapshot.LastEventAt.Should().Be(checkedAt);
        stored.Snapshot.FailureCount.Should().Be(1);
        stored.Snapshot.LastError.Should().Be("Transient");
    }

    private BotOptions CreateOptions() =>
        new()
        {
            Storage = new StorageOptions
            {
                DatabasePath = Path.Combine(
                    _tempDirectory,
                    "observers.db"),
            },
        };

    private static PageObserverDefinition CreateDefinition() =>
        new()
        {
            ObserverId = Guid.Parse(
                "af495d83-74bd-4fa8-91a3-36441f953abc"),
            Name = "Inventory",
            Url = "https://example.test/inventory",
            BrowserProfile = "shop",
            Condition = PageObserverConditionKind.TextEquals,
            Locator = ScenarioLocator.FromSelector("#stock"),
            ExpectedValue = "Available",
            EventType = "inventory.changed",
            CorrelationId = "sku-42",
            PollIntervalMs = 15000,
            Enabled = true,
        };

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
