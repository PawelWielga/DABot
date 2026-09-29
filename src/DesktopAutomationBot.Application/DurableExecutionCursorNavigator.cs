using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

internal static class DurableExecutionCursorNavigator
{
    public static (ScenarioStep Step, int Index) ResolveStep(
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

        var siblings = scenario.Steps;
        foreach (var frame in cursor.Frames)
        {
            var container = FindById(siblings, frame.StepId)
                ?? throw new InvalidOperationException(
                    $"Execution cursor container '{frame.StepId}' does not exist.");

            if (container.Type is not StepType.If and not StepType.Loop)
            {
                throw new InvalidOperationException(
                    $"Execution cursor container '{frame.StepId}' is not a control-flow step.");
            }

            siblings = container.Children;
        }

        for (var index = 0; index < siblings.Count; index++)
        {
            if (string.Equals(
                    siblings[index].Id,
                    cursor.NextStepId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return (siblings[index], index);
            }
        }

        throw new InvalidOperationException(
            $"Execution cursor step '{cursor.NextStepId}' does not match its active frame path.");
    }

    public static (ScenarioStep Step, int Index) ResolveTopLevelStep(
        ScenarioDefinition scenario,
        ExecutionCursor cursor) => ResolveStep(scenario, cursor);

    public static ExecutionCursor EnterIf(
        ScenarioStep step,
        ExecutionCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(cursor);

        if (step.Type != StepType.If || step.Children.Count == 0)
        {
            throw new InvalidOperationException(
                "Only a non-empty If step can be entered.");
        }

        return cursor with
        {
            NextStepId = step.Children[0].Id,
            Frames =
            [
                .. cursor.Frames,
                new ExecutionFrame
                {
                    StepId = step.Id!,
                    Kind = ExecutionFrameKind.If,
                    NextChildIndex = 0,
                },
            ],
        };
    }

    public static ExecutionCursor AdvanceCursor(
        ScenarioDefinition scenario,
        ExecutionCursor cursor,
        int completedStepIndex)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(cursor);

        if (cursor.Frames.Count == 0)
        {
            return AdvanceTopLevelCursor(scenario, completedStepIndex);
        }

        var frames = cursor.Frames.ToList();
        var activeFrame = frames[^1];
        var parent = FindById(scenario.Steps, activeFrame.StepId)
            ?? throw new InvalidOperationException(
                $"Execution cursor container '{activeFrame.StepId}' does not exist.");

        var nextChildIndex = completedStepIndex + 1;
        if (nextChildIndex < parent.Children.Count)
        {
            frames[^1] = activeFrame with { NextChildIndex = nextChildIndex };
            return cursor with
            {
                NextStepId = parent.Children[nextChildIndex].Id,
                Frames = frames,
            };
        }

        frames.RemoveAt(frames.Count - 1);
        return AdvanceAfterContainer(scenario, frames, parent.Id!);
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

    private static ExecutionCursor AdvanceAfterContainer(
        ScenarioDefinition scenario,
        List<ExecutionFrame> remainingFrames,
        string completedContainerId)
    {
        var siblings = scenario.Steps;
        if (remainingFrames.Count > 0)
        {
            foreach (var frame in remainingFrames)
            {
                var container = FindById(siblings, frame.StepId)
                    ?? throw new InvalidOperationException(
                        $"Execution cursor container '{frame.StepId}' does not exist.");
                siblings = container.Children;
            }
        }

        var completedIndex = siblings
            .Select((step, index) => (step, index))
            .FirstOrDefault(item => string.Equals(
                item.step.Id,
                completedContainerId,
                StringComparison.OrdinalIgnoreCase)).index;

        if (completedIndex + 1 < siblings.Count)
        {
            if (remainingFrames.Count > 0)
            {
                var active = remainingFrames[^1];
                remainingFrames[^1] = active with
                {
                    NextChildIndex = completedIndex + 1,
                };
            }

            return new ExecutionCursor
            {
                NextStepId = siblings[completedIndex + 1].Id,
                Frames = remainingFrames,
            };
        }

        if (remainingFrames.Count == 0)
        {
            return ExecutionCursor.Completed();
        }

        var completedParent = remainingFrames[^1].StepId;
        remainingFrames.RemoveAt(remainingFrames.Count - 1);
        return AdvanceAfterContainer(
            scenario,
            remainingFrames,
            completedParent);
    }

    private static ScenarioStep? FindById(
        IReadOnlyList<ScenarioStep> steps,
        string id)
    {
        foreach (var step in steps)
        {
            if (string.Equals(step.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return step;
            }
        }

        return null;
    }
}
