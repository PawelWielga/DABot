using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopAutomationBot.Core;

public static class ExecutionCursorSchema
{
    public const int CurrentVersion = 1;
}

public enum ExecutionFrameKind
{
    If,
    Loop,
}

public sealed record ExecutionFrame
{
    public required string StepId { get; init; }

    public required ExecutionFrameKind Kind { get; init; }

    public required int NextChildIndex { get; init; }

    public int? Iteration { get; init; }
}

public sealed record ExecutionCursor
{
    public int Version { get; init; } = ExecutionCursorSchema.CurrentVersion;

    public string? NextStepId { get; init; }

    public List<ExecutionFrame> Frames { get; init; } = [];

    public bool IsCompleted => NextStepId is null && Frames.Count == 0;

    public static ExecutionCursor Start(ScenarioDefinition scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var normalized = ScenarioDefinitionNormalizer.Normalize(scenario);
        if (normalized.Steps.Count == 0)
        {
            throw new InvalidOperationException("Cannot create an execution cursor for a scenario without steps.");
        }

        return new ExecutionCursor
        {
            NextStepId = normalized.Steps[0].Id,
        };
    }

    public static ExecutionCursor Completed() => new();
}

public sealed record ExecutionCursorValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed class ExecutionCursorValidator
{
    public ExecutionCursorValidationResult Validate(ScenarioDefinition scenario, ExecutionCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(cursor);

        var errors = new List<string>();
        var normalized = ScenarioDefinitionNormalizer.Normalize(scenario);

        if (cursor.Version != ExecutionCursorSchema.CurrentVersion)
        {
            errors.Add(
                $"cursor.version '{cursor.Version}' is not supported. " +
                $"Supported version is {ExecutionCursorSchema.CurrentVersion}.");
        }

        if (cursor.NextStepId is null)
        {
            if (cursor.Frames.Count > 0)
            {
                errors.Add("cursor.frames must be empty when cursor.nextStepId is null.");
            }

            return new ExecutionCursorValidationResult(errors);
        }

        var index = BuildStepIndex(normalized);
        if (!index.TryGetValue(cursor.NextStepId, out var nextStep))
        {
            errors.Add($"cursor.nextStepId '{cursor.NextStepId}' does not exist in the scenario.");
            return new ExecutionCursorValidationResult(errors);
        }

        var expectedFrames = nextStep.Ancestors;
        if (cursor.Frames.Count != expectedFrames.Count)
        {
            errors.Add(
                $"cursor.frames count '{cursor.Frames.Count}' does not match the expected active control-flow depth '{expectedFrames.Count}' for step '{cursor.NextStepId}'.");
            return new ExecutionCursorValidationResult(errors);
        }

        for (var frameIndex = 0; frameIndex < cursor.Frames.Count; frameIndex++)
        {
            ValidateFrame(cursor.Frames[frameIndex], expectedFrames[frameIndex], frameIndex, errors);
        }

        return new ExecutionCursorValidationResult(errors);
    }

    private static void ValidateFrame(
        ExecutionFrame frame,
        AncestorInfo expected,
        int frameIndex,
        ICollection<string> errors)
    {
        if (!string.Equals(frame.StepId, expected.Step.Id, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                $"cursor.frames[{frameIndex}].stepId '{frame.StepId}' does not match expected ancestor step '{expected.Step.Id}'.");
        }

        var expectedKind = expected.Step.Type switch
        {
            StepType.If => ExecutionFrameKind.If,
            StepType.Loop => ExecutionFrameKind.Loop,
            _ => throw new InvalidOperationException(
                $"Step '{expected.Step.Id}' is not a supported control-flow container."),
        };

        if (frame.Kind != expectedKind)
        {
            errors.Add(
                $"cursor.frames[{frameIndex}].kind '{frame.Kind}' does not match step '{expected.Step.Id}' type '{expected.Step.Type}'.");
        }

        if (frame.NextChildIndex != expected.ChildIndex)
        {
            errors.Add(
                $"cursor.frames[{frameIndex}].nextChildIndex '{frame.NextChildIndex}' does not match the child path index '{expected.ChildIndex}'.");
        }

        if (frame.NextChildIndex < 0 || frame.NextChildIndex >= expected.Step.Children.Count)
        {
            errors.Add(
                $"cursor.frames[{frameIndex}].nextChildIndex '{frame.NextChildIndex}' is outside the children of step '{expected.Step.Id}'.");
        }

        if (frame.Kind == ExecutionFrameKind.Loop)
        {
            if (!frame.Iteration.HasValue || frame.Iteration.Value < 0)
            {
                errors.Add($"cursor.frames[{frameIndex}].iteration must be zero or greater for Loop frames.");
            }
        }
        else if (frame.Iteration.HasValue)
        {
            errors.Add($"cursor.frames[{frameIndex}].iteration must be null for If frames.");
        }
    }

    private static Dictionary<string, StepInfo> BuildStepIndex(ScenarioDefinition scenario)
    {
        var index = new Dictionary<string, StepInfo>(StringComparer.OrdinalIgnoreCase);
        IndexSteps(scenario.Steps, [], index);
        return index;
    }

    private static void IndexSteps(
        IReadOnlyList<ScenarioStep> steps,
        IReadOnlyList<AncestorInfo> ancestors,
        IDictionary<string, StepInfo> index)
    {
        for (var childIndex = 0; childIndex < steps.Count; childIndex++)
        {
            var step = steps[childIndex];
            var stepId = step.Id ?? throw new InvalidOperationException("Scenario must be normalized before cursor validation.");

            index.Add(stepId, new StepInfo(step, ancestors));

            if (step.Children.Count == 0)
            {
                continue;
            }

            if (step.Type is not StepType.If and not StepType.Loop)
            {
                continue;
            }

            for (var nestedIndex = 0; nestedIndex < step.Children.Count; nestedIndex++)
            {
                var nestedStep = step.Children[nestedIndex];
                var nestedId = nestedStep.Id ?? throw new InvalidOperationException("Scenario must be normalized before cursor validation.");

                var nestedAncestors = ancestors
                    .Concat([new AncestorInfo(step, nestedIndex)])
                    .ToArray();

                index.Add(nestedId, new StepInfo(nestedStep, nestedAncestors));

                IndexDescendants(nestedStep, nestedAncestors, index);
            }
        }
    }

    private static void IndexDescendants(
        ScenarioStep parent,
        IReadOnlyList<AncestorInfo> ancestors,
        IDictionary<string, StepInfo> index)
    {
        if (parent.Children.Count == 0 || parent.Type is not StepType.If and not StepType.Loop)
        {
            return;
        }

        for (var childIndex = 0; childIndex < parent.Children.Count; childIndex++)
        {
            var child = parent.Children[childIndex];
            var childId = child.Id ?? throw new InvalidOperationException("Scenario must be normalized before cursor validation.");
            var childAncestors = ancestors
                .Concat([new AncestorInfo(parent, childIndex)])
                .ToArray();

            index.Add(childId, new StepInfo(child, childAncestors));
            IndexDescendants(child, childAncestors, index);
        }
    }

    private sealed record StepInfo(ScenarioStep Step, IReadOnlyList<AncestorInfo> Ancestors);

    private sealed record AncestorInfo(ScenarioStep Step, int ChildIndex);
}

public static class ExecutionCursorJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(ExecutionCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        return JsonSerializer.Serialize(cursor, SerializerOptions);
    }

    public static ExecutionCursor Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        return JsonSerializer.Deserialize<ExecutionCursor>(json, SerializerOptions)
            ?? throw new JsonException("Execution cursor JSON could not be parsed.");
    }
}
