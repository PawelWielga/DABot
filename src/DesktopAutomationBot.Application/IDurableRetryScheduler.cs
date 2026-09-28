namespace DesktopAutomationBot.Application;

public interface IDurableRetryScheduler
{
    Task<IReadOnlyList<DurableScenarioExecutionResult>> RunDueRetriesAsync(
        int maxRuns = 100,
        CancellationToken cancellationToken = default);
}
