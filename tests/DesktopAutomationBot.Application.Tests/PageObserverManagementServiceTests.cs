using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class PageObserverManagementServiceTests
{
    [Fact]
    public async Task SaveAsync_WhenDefinitionIsInvalid_ReturnsValidationError()
    {
        var store = new RecordingStore();
        var service = new PageObserverManagementService(store);

        var result = await service.SaveAsync(
            CreateDefinition() with
            {
                PollIntervalMs = 0,
            });

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        store.SavedDefinition.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_WhenCreatingObserver_DoesNotRequestSnapshotReset()
    {
        var store = new RecordingStore();
        var service = new PageObserverManagementService(store);
        var definition = CreateDefinition();

        var result = await service.SaveAsync(definition);

        result.Success.Should().BeTrue();
        store.SavedDefinition.Should().Be(definition);
        store.ResetSnapshot.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_WhenOnlyMetadataOrSchedulingChanges_PreservesSnapshot()
    {
        var existing = CreateDefinition();
        var store = new RecordingStore(existing);
        var service = new PageObserverManagementService(store);

        var result = await service.SaveAsync(
            existing with
            {
                Name = "Renamed observer",
                PollIntervalMs = 30000,
                Enabled = false,
            });

        result.Success.Should().BeTrue();
        store.ResetSnapshot.Should().BeFalse();
    }

    [Theory]
    [InlineData("url")]
    [InlineData("profile")]
    [InlineData("condition")]
    [InlineData("locator")]
    [InlineData("expected")]
    [InlineData("eventType")]
    [InlineData("correlation")]
    public async Task SaveAsync_WhenObservationSemanticsChange_ResetsSnapshot(
        string change)
    {
        var existing = CreateDefinition();
        var updated = change switch
        {
            "url" => existing with
            {
                Url = "https://example.test/other",
            },
            "profile" => existing with
            {
                BrowserProfile = "other-profile",
            },
            "condition" => existing with
            {
                Condition = PageObserverConditionKind.TextContains,
            },
            "locator" => existing with
            {
                Locator = ScenarioLocator.FromSelector("#other"),
            },
            "expected" => existing with
            {
                ExpectedValue = "Ready",
            },
            "eventType" => existing with
            {
                EventType = "inventory.ready",
            },
            "correlation" => existing with
            {
                CorrelationId = "sku-99",
            },
            _ => throw new ArgumentOutOfRangeException(nameof(change)),
        };

        var store = new RecordingStore(existing);
        var service = new PageObserverManagementService(store);

        var result = await service.SaveAsync(updated);

        result.Success.Should().BeTrue();
        store.ResetSnapshot.Should().BeTrue();
    }

    private static PageObserverDefinition CreateDefinition() =>
        new()
        {
            ObserverId = Guid.Parse(
                "e8478da4-d349-4ea9-9f16-91c4b43b6a4d"),
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

    private sealed class RecordingStore(
        PageObserverDefinition? existing = null)
        : IPageObserverManagementStore
    {
        public PageObserverDefinition? SavedDefinition { get; private set; }

        public bool? ResetSnapshot { get; private set; }

        public Task<PageObserverDefinition?> GetDefinitionAsync(
            Guid observerId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                existing?.ObserverId == observerId
                    ? existing
                    : null);
        }

        public Task SaveDefinitionAsync(
            PageObserverDefinition definition,
            bool resetSnapshot,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SavedDefinition = definition;
            ResetSnapshot = resetSnapshot;
            return Task.CompletedTask;
        }
    }
}
