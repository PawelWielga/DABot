using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public sealed class ReadTextStepHandler : IStepHandler
{
    public StepType StepType => StepType.ReadText;

    public async Task<StepExecutionResult> ExecuteAsync(ScenarioStep step, ScenarioExecutionContext context, int index, CancellationToken cancellationToken)
    {
        var outputValue = await context.BrowserAutomation.ReadTextAsync(step.Selector!, step.TimeoutMs, cancellationToken);
        context.Variables.Set(step.Output!, outputValue);

        return new StepExecutionResult
        {
            Index = index,
            Type = StepType,
            Success = true,
            OutputName = step.Output,
            OutputValue = outputValue,
        };
    }
}
