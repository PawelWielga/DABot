using System.Globalization;
using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public sealed class DelayStepHandler : IStepHandler
{
    public StepType StepType => StepType.Delay;

    public async Task<StepExecutionResult> ExecuteAsync(
        ScenarioStep step,
        ScenarioExecutionContext context,
        int index,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(context);

        var delayMs = ResolveDelayMilliseconds(step);
        await Task.Delay(delayMs, cancellationToken);

        return new StepExecutionResult
        {
            Index = index,
            Type = StepType,
            Success = true,
        };
    }

    private static int ResolveDelayMilliseconds(ScenarioStep step)
    {
        if (step.TimeoutMs is int timeoutMs)
        {
            if (timeoutMs < 0)
            {
                throw new InvalidOperationException(
                    "Delay timeoutMs must be zero or greater.");
            }

            return timeoutMs;
        }

        if (int.TryParse(
                step.Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var valueMs) &&
            valueMs >= 0)
        {
            return valueMs;
        }

        throw new InvalidOperationException(
            "Delay requires timeoutMs or value containing a non-negative integer number of milliseconds.");
    }
}
