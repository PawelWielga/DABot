using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public interface IDurableRunControlService
{
    Task<DurableScenarioExecutionResult> CloneAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    Task<DurableScenarioExecutionResult> ResumeManuallyAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    Task<DurableScenarioExecutionResult> RetryNowAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    Task<AutomationRun> CancelAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
}
