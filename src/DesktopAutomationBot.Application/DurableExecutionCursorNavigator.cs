using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

internal static class DurableExecutionCursorNavigator
{
    public static (ScenarioStep Step, int Index) ResolveTopLevelStep(
        ScenarioDefinition scenario,
        ExecutionCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(cursor);

        if (cursor.IsCompleted)
        {
            throw new InvalidOperationException(
                "Completed execution cursor does not identify a next step.");
        }

        if (cursor.Frames.Count > 0)
        {
            throw new NotSupportedException(
                "Durable nested If/Loop execution is not implemented yet.");
        }

        for (var index = 0; index < scenario.Steps.Count; index++)
        {
            var step = scenario.Steps[index];

            if (string.Equals(
                    step.Id,
                    cursor.NextStepId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return (step, index);
            }
        }

        throw new InvalidOperationException(
            $"Execution cursor step '{cursor.NextStepId}' is not a top-level scenario step.");
    }

    public static ExecutionCursor AdvanceTopLevelCursor(
        ScenarioDefinition scenario,
        int completedStepIndex)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var nextIndex = completedStepIndex + 1;

        if (nextIndex >= scenario.Steps.Count)
        {
            return ExecutionCursor.Completed();
        }

        return new ExecutionCursor
        {
            NextStepId = scenario.Steps[nextIndex].Id,
        };
    }
}
