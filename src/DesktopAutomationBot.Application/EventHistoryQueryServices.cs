using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public sealed record EventHistoryItem
{
    public required Guid EventId { get; init; }

    public required string Type { get; init; }

    public required string CorrelationId { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }

    public Guid? RunId { get; init; }

    public Guid? WorkItemId { get; init; }

    public ResumeWorkItemStatus? WorkItemStatus { get; init; }

    public int? AttemptCount { get; init; }

    public DateTimeOffset? NextAttemptAt { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }

    public string? ErrorMessage { get; init; }

    public bool MatchedWaitingRun => RunId is not null;
}

public interface IEventHistoryQueryService
{
    Task<IReadOnlyList<EventHistoryItem>> ListRecentAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);
}
