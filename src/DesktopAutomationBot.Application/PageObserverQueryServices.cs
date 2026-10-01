using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public sealed record PageObserverListItem
{
    public required Guid ObserverId { get; init; }

    public required string Name { get; init; }

    public required string Url { get; init; }

    public string? BrowserProfile { get; init; }

    public required PageObserverConditionKind Condition { get; init; }

    public required string EventType { get; init; }

    public required string CorrelationId { get; init; }

    public required int PollIntervalMs { get; init; }

    public required bool Enabled { get; init; }

    public DateTimeOffset? LastCheckedAt { get; init; }

    public DateTimeOffset? NextCheckAt { get; init; }

    public DateTimeOffset? LastEventAt { get; init; }

    public required int FailureCount { get; init; }

    public string? LastError { get; init; }
}

public interface IPageObserverQueryService
{
    Task<IReadOnlyList<PageObserverListItem>> ListAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);
}
