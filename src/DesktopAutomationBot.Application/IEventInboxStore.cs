using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public sealed record EventAcceptanceResult
{
    public required bool IsDuplicate { get; init; }

    public required bool MatchedWaitingRun { get; init; }

    public Guid? RunId { get; init; }

    public Guid? ResumeWorkItemId { get; init; }
}

public interface IEventInboxStore
{
    Task ArmEventWaitAsync(
        AutomationRun run,
        ScenarioVersion scenarioVersion,
        EventWaitRegistration wait,
        CancellationToken cancellationToken = default);

    Task<EventAcceptanceResult> AcceptAsync(
        AutomationEvent automationEvent,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResumeWorkItem>> LoadPendingResumeWorkItemsAsync(
        DateTimeOffset dueAt,
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResumeWorkItem>> LoadDeadLetterResumeWorkItemsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task MarkResumeWorkItemCompletedAsync(
        Guid workItemId,
        DateTimeOffset finishedAt,
        CancellationToken cancellationToken = default);

    Task ScheduleResumeWorkItemRetryAsync(
        Guid workItemId,
        string errorMessage,
        DateTimeOffset nextAttemptAt,
        CancellationToken cancellationToken = default);

    Task DeadLetterResumeWorkItemAsync(
        Guid workItemId,
        string errorMessage,
        DateTimeOffset finishedAt,
        CancellationToken cancellationToken = default);
}
