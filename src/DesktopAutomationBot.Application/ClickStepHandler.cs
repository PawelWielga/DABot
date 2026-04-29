using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public sealed class ClickStepHandler : IStepHandler
{
    public StepType StepType => StepType.Click;

    public async Task<StepExecutionResult> ExecuteAsync(ScenarioStep step, ScenarioExecutionContext context, int index, CancellationToken cancellationToken)
    {
        await context.BrowserAutomation.ClickAsync(step.Selector!, step.TimeoutMs, cancellationToken);

        return new StepExecutionResult
        {
            Index = index,
            Type = StepType,
            Success = true,
        };
    }
}
