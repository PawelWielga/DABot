using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public sealed class OpenUrlStepHandler : IStepHandler
{
    public StepType StepType => StepType.OpenUrl;

    public async Task<StepExecutionResult> ExecuteAsync(ScenarioStep step, ScenarioExecutionContext context, int index, CancellationToken cancellationToken)
    {
        await context.BrowserAutomation.NavigateAsync(step.Url!, step.TimeoutMs, cancellationToken);

        return new StepExecutionResult
        {
            Index = index,
            Type = StepType,
            Success = true,
        };
    }
}
