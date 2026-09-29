using System.Globalization;
using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

internal static class ControlFlowStepEvaluator
{
    public static bool EvaluateIf(
        ScenarioStep step,
        ScenarioVariableBag variables)
    {
        var resolved = ScenarioVariableInterpolator.ResolveText(
            step.Value,
            variables);

        return bool.TryParse(resolved, out var result)
            ? result
            : throw new InvalidOperationException(
                $"If step '{step.Id}' value must resolve to 'true' or 'false'.");
    }

    public static int GetLoopCount(
        ScenarioStep step,
        ScenarioVariableBag variables)
    {
        var resolved = ScenarioVariableInterpolator.ResolveText(
            step.Value,
            variables);

        return int.TryParse(
                resolved,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var count) &&
            count >= 0
            ? count
            : throw new InvalidOperationException(
                $"Loop step '{step.Id}' value must resolve to a non-negative integer.");
    }
}
