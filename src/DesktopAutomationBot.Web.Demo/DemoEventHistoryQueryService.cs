using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Web.Demo;

public sealed class DemoEventHistoryQueryService :
    IEventHistoryQueryService
{
    private static readonly IReadOnlyList<EventHistoryItem> Events =
        CreateEvents();

    public Task<IReadOnlyList<EventHistoryItem>> ListRecentAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                limit,
                "Limit must be greater than zero.");
        }

        return Task.FromResult<IReadOnlyList<EventHistoryItem>>(
            Events
                .OrderByDescending(item => item.ReceivedAt)
                .Take(limit)
                .ToArray());
    }

    private static IReadOnlyList<EventHistoryItem> CreateEvents()
    {
        var now = DateTimeOffset.UtcNow;

        return
        [
            new()
            {
                EventId = Guid.Parse("81000000-0000-4000-8000-000000000001"),
                Type = "approval.completed",
                CorrelationId = "approval-123",
                OccurredAt = now.AddMinutes(-6),
                ReceivedAt = now.AddMinutes(-5),
                RunId = Guid.Parse("d4c44444-4444-4444-8444-444444444444"),
                WorkItemId = Guid.Parse("82000000-0000-4000-8000-000000000001"),
                WorkItemStatus = ResumeWorkItemStatus.Completed,
                AttemptCount = 1,
                FinishedAt = now.AddMinutes(-5).AddSeconds(8),
            },
            new()
            {
                EventId = Guid.Parse("81000000-0000-4000-8000-000000000002"),
                Type = "inventory.changed",
                CorrelationId = "inventory-demo",
                OccurredAt = now.AddMinutes(-13),
                ReceivedAt = now.AddMinutes(-12),
            },
            new()
            {
                EventId = Guid.Parse("81000000-0000-4000-8000-000000000003"),
                Type = "order.updated",
                CorrelationId = "order-demo-42",
                OccurredAt = now.AddMinutes(-23),
                ReceivedAt = now.AddMinutes(-22),
                RunId = Guid.Parse("e5c55555-5555-4555-8555-555555555555"),
                WorkItemId = Guid.Parse("82000000-0000-4000-8000-000000000003"),
                WorkItemStatus = ResumeWorkItemStatus.DeadLetter,
                AttemptCount = 5,
                FinishedAt = now.AddMinutes(-20),
                ErrorMessage = "Demo resume failure after maximum attempts.",
            },
        ];
    }
}
