using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public interface IStepHandler
{
    StepType StepType { get; }

    Task<StepExecutionResult> ExecuteAsync(ScenarioStep step, ScenarioExecutionContext context, int index, CancellationToken cancellationToken);
}
