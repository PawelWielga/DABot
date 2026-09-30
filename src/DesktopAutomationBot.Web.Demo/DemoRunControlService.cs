using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Web.Demo;

public sealed class DemoRunControlService : IDurableRunControlService
{
    private const string Message =
        "Run actions are disabled in the static GitHub Pages demo.";

    public Task<DurableScenarioExecutionResult> ResumeManuallyAsync(
        Guid runId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(Message);

    public Task<DurableScenarioExecutionResult> RetryNowAsync(
        Guid runId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(Message);

    public Task<AutomationRun> CancelAsync(
        Guid runId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(Message);
}
