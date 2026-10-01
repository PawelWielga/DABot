using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Web.Demo;

public sealed class DemoPageObserverQueryService :
    IPageObserverQueryService,
    IPageObserverManagementStore
{
    private readonly Dictionary<Guid, DemoObserverState> _states =
        CreateStates();

    public Task<IReadOnlyList<PageObserverListItem>> ListAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var items = _states.Values
            .OrderByDescending(state => state.Definition.Enabled)
            .ThenBy(
                state => state.Definition.Name,
                StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(ToListItem)
            .ToArray();

        return Task.FromResult<IReadOnlyList<PageObserverListItem>>(items);
    }

    public Task<PageObserverDefinition?> GetDefinitionAsync(
        Guid observerId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (observerId == Guid.Empty)
        {
            throw new ArgumentException(
                "Observer ID must not be empty.",
                nameof(observerId));
        }

        return Task.FromResult(
            _states.TryGetValue(observerId, out var state)
                ? state.Definition
                : null);
    }

    public Task SaveDefinitionAsync(
        PageObserverDefinition definition,
        bool resetSnapshot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validated = PageObserverDefinition.Validate(definition);

        var snapshot =
            _states.TryGetValue(validated.ObserverId, out var existing) &&
            !resetSnapshot
                ? existing.Snapshot
                : new PageObserverSnapshot
                {
                    ObserverId = validated.ObserverId,
                };

        _states[validated.ObserverId] =
            new DemoObserverState(validated, snapshot);

        return Task.CompletedTask;
    }

    private static PageObserverListItem ToListItem(
        DemoObserverState state)
    {
        var definition = state.Definition;
        var snapshot = state.Snapshot;

        return new PageObserverListItem
        {
            ObserverId = definition.ObserverId,
            Name = definition.Name,
            Url = definition.Url,
            BrowserProfile = definition.BrowserProfile,
            Condition = definition.Condition,
            EventType = definition.EventType,
            CorrelationId = definition.CorrelationId,
            PollIntervalMs = definition.PollIntervalMs,
            Enabled = definition.Enabled,
            LastCheckedAt = snapshot.LastCheckedAt,
            NextCheckAt = snapshot.NextCheckAt,
            LastEventAt = snapshot.LastEventAt,
            FailureCount = snapshot.FailureCount,
            LastError = snapshot.LastError,
        };
    }

    private static Dictionary<Guid, DemoObserverState> CreateStates()
    {
        var inventoryId =
            Guid.Parse("7bd693bf-53bb-42e2-9e81-9d6ac4869a31");
        var approvalId =
            Guid.Parse("0cbdd7e8-a30d-4bdf-bc9f-aa4d398ab0e0");
        var legacyId =
            Guid.Parse("4cfefcff-c4ab-447c-8504-0a1e24b25c94");

        return new Dictionary<Guid, DemoObserverState>
        {
            [inventoryId] = new(
                new PageObserverDefinition
                {
                    ObserverId = inventoryId,
                    Name = "Inventory availability",
                    Url = "https://example.test/products/widget",
                    BrowserProfile = "shop-account",
                    Condition = PageObserverConditionKind.TextChanged,
                    Locator = ScenarioLocator.FromSelector("#stock"),
                    EventType = "inventory.changed",
                    CorrelationId = "widget-42",
                    PollIntervalMs = 15000,
                    Enabled = true,
                },
                new PageObserverSnapshot
                {
                    ObserverId = inventoryId,
                    LastObservation = "5 available",
                    LastMatched = false,
                    LastCheckedAt = DateTimeOffset.Parse(
                        "2026-10-01T16:42:00+02:00"),
                    NextCheckAt = DateTimeOffset.Parse(
                        "2026-10-01T16:42:15+02:00"),
                    LastEventAt = DateTimeOffset.Parse(
                        "2026-10-01T16:30:00+02:00"),
                }),
            [approvalId] = new(
                new PageObserverDefinition
                {
                    ObserverId = approvalId,
                    Name = "Approval banner",
                    Url = "https://example.test/approvals/123",
                    Condition = PageObserverConditionKind.SelectorVisible,
                    Locator = ScenarioLocator.FromSelector(
                        "[data-status='ready']"),
                    EventType = "approval.ready",
                    CorrelationId = "approval-123",
                    PollIntervalMs = 5000,
                    Enabled = true,
                },
                new PageObserverSnapshot
                {
                    ObserverId = approvalId,
                    LastCheckedAt = DateTimeOffset.Parse(
                        "2026-10-01T16:41:55+02:00"),
                    NextCheckAt = DateTimeOffset.Parse(
                        "2026-10-01T16:42:00+02:00"),
                    FailureCount = 2,
                    LastError = "Navigation timed out.",
                }),
            [legacyId] = new(
                new PageObserverDefinition
                {
                    ObserverId = legacyId,
                    Name = "Legacy status watcher",
                    Url = "https://example.test/legacy/status",
                    Condition = PageObserverConditionKind.UrlMatches,
                    ExpectedValue = "/legacy/status$",
                    EventType = "legacy.status",
                    CorrelationId = "legacy",
                    PollIntervalMs = 30000,
                    Enabled = false,
                },
                new PageObserverSnapshot
                {
                    ObserverId = legacyId,
                }),
        };
    }

    private sealed record DemoObserverState(
        PageObserverDefinition Definition,
        PageObserverSnapshot Snapshot);
}
