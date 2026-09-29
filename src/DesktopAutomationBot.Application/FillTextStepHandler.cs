using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public sealed class FillTextStepHandler : IStepHandler
{
    public StepType StepType => StepType.FillText;

    public async Task<StepExecutionResult> ExecuteAsync(ScenarioStep step, ScenarioExecutionContext context, int index, CancellationToken cancellationToken)
    {
        await context.BrowserAutomation.FillTextAsync(ScenarioStepLocator.Resolve(step), step.Value ?? string.Empty, step.TimeoutMs, cancellationToken);

        return new StepExecutionResult
        {
            Index = index,
            Type = StepType,
            Success = true,
        };
    }
}
