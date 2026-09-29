using System.Text.Json;
using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

internal static class SuspendStepEvaluator
{
    public static RunWaitReason GetWaitReason(ScenarioStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        if (step.Type != StepType.Suspend)
        {
            throw new ArgumentException(
                "Suspend wait reason can only be evaluated for Suspend steps.",
                nameof(step));
        }

        if (step.Parameters is null ||
            !step.Parameters.TryGetValue("reason", out var rawReason))
        {
            return RunWaitReason.Human;
        }

        if (rawReason.ValueKind != JsonValueKind.String ||
            !Enum.TryParse<RunWaitReason>(
                rawReason.GetString(),
                ignoreCase: true,
                out var reason) ||
            reason == RunWaitReason.Retry)
        {
            throw new InvalidOperationException(
                "Suspend reason must be Human, Event, or Schedule.");
        }

        return reason;
    }
}
