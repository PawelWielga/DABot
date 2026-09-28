namespace DesktopAutomationBot.Application;

public interface IDurableRunResumeService
{
    Task<DurableScenarioExecutionResult> ResumeAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
}
