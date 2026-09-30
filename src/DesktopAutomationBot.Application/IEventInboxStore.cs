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
        int limit = 100,
        CancellationToken cancellationToken = default);
}
