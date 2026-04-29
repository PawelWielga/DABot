using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public sealed class PasteTextStepHandler : IStepHandler
{
    public StepType StepType => StepType.PasteText;

    public async Task<StepExecutionResult> ExecuteAsync(ScenarioStep step, ScenarioExecutionContext context, int index, CancellationToken cancellationToken)
    {
        await context.BrowserAutomation.PasteTextAsync(step.Selector!, step.Value ?? string.Empty, step.TimeoutMs, cancellationToken);

        return new StepExecutionResult
        {
            Index = index,
            Type = StepType,
            Success = true,
        };
    }
}
