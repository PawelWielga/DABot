using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Web.Demo;

public sealed class DemoPageObserverQueryService : IPageObserverQueryService
{
    private static readonly IReadOnlyList<PageObserverListItem> Items =
    [
        new()
        {
            ObserverId = Guid.Parse("7bd693bf-53bb-42e2-9e81-9d6ac4869a31"),
            Name = "Inventory availability",
            Url = "https://example.test/products/widget",
            BrowserProfile = "shop-account",
            Condition = PageObserverConditionKind.TextChanged,
            EventType = "inventory.changed",
            CorrelationId = "widget-42",
            PollIntervalMs = 15000,
            Enabled = true,
            LastCheckedAt = DateTimeOffset.Parse("2026-10-01T16:42:00+02:00"),
            NextCheckAt = DateTimeOffset.Parse("2026-10-01T16:42:15+02:00"),
            LastEventAt = DateTimeOffset.Parse("2026-10-01T16:30:00+02:00"),
            FailureCount = 0,
        },
        new()
        {
            ObserverId = Guid.Parse("0cbdd7e8-a30d-4bdf-bc9f-aa4d398ab0e0"),
            Name = "Approval banner",
            Url = "https://example.test/approvals/123",
            Condition = PageObserverConditionKind.SelectorVisible,
            EventType = "approval.ready",
            CorrelationId = "approval-123",
            PollIntervalMs = 5000,
            Enabled = true,
            LastCheckedAt = DateTimeOffset.Parse("2026-10-01T16:41:55+02:00"),
            NextCheckAt = DateTimeOffset.Parse("2026-10-01T16:42:00+02:00"),
            FailureCount = 2,
            LastError = "Navigation timed out.",
        },
        new()
        {
            ObserverId = Guid.Parse("4cfefcff-c4ab-447c-8504-0a1e24b25c94"),
            Name = "Legacy status watcher",
            Url = "https://example.test/legacy/status",
            Condition = PageObserverConditionKind.UrlMatches,
            EventType = "legacy.status",
            CorrelationId = "legacy",
            PollIntervalMs = 30000,
            Enabled = false,
            FailureCount = 0,
        },
    ];

    public Task<IReadOnlyList<PageObserverListItem>> ListAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        return Task.FromResult<IReadOnlyList<PageObserverListItem>>(
            Items.Take(limit).ToArray());
    }
}
