using System.Collections.ObjectModel;
using System.Text.Json;

namespace DesktopAutomationBot.Core;

public sealed record CompiledScenario
{
    public required int SchemaVersion { get; init; }

    public required string Name { get; init; }

    public int? TimeoutMs { get; init; }

    public required IReadOnlyList<CompiledStep> Steps { get; init; }
}

public abstract record CompiledStep
{
    public required string Id { get; init; }

    public required StepType Type { get; init; }

    public int? TimeoutMs { get; init; }

    public int RetryCount { get; init; }

    public int RetryDelayMs { get; init; }

    public StepRetrySafety? RetrySafety { get; init; }

    public required IReadOnlyDictionary<string, JsonElement> Parameters { get; init; }
}

public sealed record CompiledActionStep : CompiledStep
{
    public string? Selector { get; init; }

    public string? Url { get; init; }

    public string? Value { get; init; }

    public string? Output { get; init; }
}

public sealed record CompiledIfStep : CompiledStep
{
    public required string Condition { get; init; }

    public required IReadOnlyList<CompiledStep> Children { get; init; }
}

public sealed record CompiledLoopStep : CompiledStep
{
    public required string Count { get; init; }

    public required IReadOnlyList<CompiledStep> Children { get; init; }
}

public static class ScenarioCompiler
{
    public static CompiledScenario Compile(ScenarioDefinition scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var normalized = ScenarioDefinitionNormalizer.Normalize(scenario);
        var validation = new ScenarioDefinitionValidator().Validate(normalized);
        if (!validation.IsValid)
        {
            throw new ScenarioValidationException(validation.Errors);
        }

        return new CompiledScenario
        {
            SchemaVersion = normalized.SchemaVersion,
            Name = normalized.Name,
            TimeoutMs = normalized.TimeoutMs,
            Steps = CompileSteps(normalized.Steps),
        };
    }

    public static ScenarioDefinition Materialize(CompiledScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        return new ScenarioDefinition
        {
            SchemaVersion = scenario.SchemaVersion,
            Name = scenario.Name,
            TimeoutMs = scenario.TimeoutMs,
            Steps = scenario.Steps.Select(MaterializeStep).ToList(),
        };
    }

    private static IReadOnlyList<CompiledStep> CompileSteps(
        IReadOnlyList<ScenarioStep> steps) =>
        Array.AsReadOnly(steps.Select(CompileStep).ToArray());

    private static CompiledStep CompileStep(ScenarioStep step)
    {
        var common = new
        {
            Id = step.Id!,
            Type = step.Type,
            step.TimeoutMs,
            RetryCount = step.RetryCount ?? 0,
            RetryDelayMs = step.RetryDelayMs ?? 0,
            step.RetrySafety,
            Parameters = CopyParameters(step.Parameters),
        };

        return step.Type switch
        {
            StepType.If => new CompiledIfStep
            {
                Id = common.Id,
                Type = common.Type,
                TimeoutMs = common.TimeoutMs,
                RetryCount = common.RetryCount,
                RetryDelayMs = common.RetryDelayMs,
                RetrySafety = common.RetrySafety,
                Parameters = common.Parameters,
                Condition = step.Value!,
                Children = CompileSteps(step.Children),
            },
            StepType.Loop => new CompiledLoopStep
            {
                Id = common.Id,
                Type = common.Type,
                TimeoutMs = common.TimeoutMs,
                RetryCount = common.RetryCount,
                RetryDelayMs = common.RetryDelayMs,
                RetrySafety = common.RetrySafety,
                Parameters = common.Parameters,
                Count = step.Value!,
                Children = CompileSteps(step.Children),
            },
            _ => new CompiledActionStep
            {
                Id = common.Id,
                Type = common.Type,
                TimeoutMs = common.TimeoutMs,
                RetryCount = common.RetryCount,
                RetryDelayMs = common.RetryDelayMs,
                RetrySafety = common.RetrySafety,
                Parameters = common.Parameters,
                Selector = step.Selector,
                Url = step.Url,
                Value = step.Value,
                Output = step.Output,
            },
        };
    }

    private static ScenarioStep MaterializeStep(CompiledStep step)
    {
        var common = new ScenarioStep
        {
            Id = step.Id,
            Type = step.Type,
            TimeoutMs = step.TimeoutMs,
            RetryCount = step.RetryCount,
            RetryDelayMs = step.RetryDelayMs,
            RetrySafety = step.RetrySafety,
            Parameters = step.Parameters.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Clone(),
                StringComparer.Ordinal),
        };

        return step switch
        {
            CompiledIfStep conditional => common with
            {
                Value = conditional.Condition,
                Children = conditional.Children.Select(MaterializeStep).ToList(),
            },
            CompiledLoopStep loop => common with
            {
                Value = loop.Count,
                Children = loop.Children.Select(MaterializeStep).ToList(),
            },
            CompiledActionStep action => common with
            {
                Selector = action.Selector,
                Url = action.Url,
                Value = action.Value,
                Output = action.Output,
            },
            _ => throw new NotSupportedException(
                $"Compiled step type '{step.GetType().Name}' is not supported."),
        };
    }

    private static IReadOnlyDictionary<string, JsonElement> CopyParameters(
        IReadOnlyDictionary<string, JsonElement>? parameters)
    {
        var copy = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (parameters is not null)
        {
            foreach (var (key, value) in parameters)
            {
                copy.Add(key, value.Clone());
            }
        }

        return new ReadOnlyDictionary<string, JsonElement>(copy);
    }
}
