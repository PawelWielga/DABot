using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using DesktopAutomationBot.Infrastructure;
using FluentAssertions;

namespace DesktopAutomationBot.Infrastructure.Tests;

public sealed class SqlitePageObserverQueryServiceTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(
            Path.GetTempPath(),
            "dabot-observer-query-tests",
            Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ListAsync_ReturnsDefinitionsAndSnapshots()
    {
        var options = CreateOptions();
        var store = new SqliteRunStore(options);
        var query = new SqlitePageObserverQueryService(options);
        var observerId = Guid.NewGuid();

        await store.SaveAsync(
            new PageObserverDefinition
            {
                ObserverId = observerId,
                Name = "Inventory",
                Url = "https://example.test/inventory",
                BrowserProfile = "shop",
                Condition = PageObserverConditionKind.TextChanged,
                Locator = new ScenarioLocator
                {
                    Strategy = ScenarioLocatorStrategy.Selector,
                    Value = "#stock",
                },
                EventType = "inventory.changed",
                CorrelationId = "sku-42",
                PollIntervalMs = 15000,
                Enabled = true,
            });

        var lastCheckedAt = DateTimeOffset.Parse("2026-10-01T14:00:00+00:00");
        var nextCheckAt = lastCheckedAt.AddSeconds(15);

        await store.SaveSnapshotAsync(
            new PageObserverSnapshot
            {
                ObserverId = observerId,
                LastObservation = "5",
                LastMatched = false,
                LastCheckedAt = lastCheckedAt,
                NextCheckAt = nextCheckAt,
                FailureCount = 1,
                LastError = "Temporary failure",
            });

        var items = await query.ListAsync();

        items.Should().ContainSingle();
        var item = items[0];
        item.ObserverId.Should().Be(observerId);
        item.Name.Should().Be("Inventory");
        item.BrowserProfile.Should().Be("shop");
        item.Condition.Should().Be(PageObserverConditionKind.TextChanged);
        item.EventType.Should().Be("inventory.changed");
        item.CorrelationId.Should().Be("sku-42");
        item.PollIntervalMs.Should().Be(15000);
        item.Enabled.Should().BeTrue();
        item.LastCheckedAt.Should().Be(lastCheckedAt);
        item.NextCheckAt.Should().Be(nextCheckAt);
        item.FailureCount.Should().Be(1);
        item.LastError.Should().Be("Temporary failure");
    }

    [Fact]
    public async Task ListAsync_WhenSchemaDoesNotExist_ReturnsEmptyList()
    {
        var query = new SqlitePageObserverQueryService(CreateOptions());

        var items = await query.ListAsync();

        items.Should().BeEmpty();
    }

    private BotOptions CreateOptions() =>
        new()
        {
            Storage = new StorageOptions
            {
                DatabasePath = Path.Combine(_tempDirectory, "observers.db"),
            },
        };

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
