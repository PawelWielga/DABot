using System.Text.Json;

namespace DesktopAutomationBot.Core;

public sealed record ScenarioDefinition
{
    public string Name { get; init; } = string.Empty;

    public List<ScenarioStep> Steps { get; init; } = [];
}

public sealed record ScenarioStep
{
    public StepType Type { get; init; }

    public string? Selector { get; init; }

    public string? Url { get; init; }

    public string? Value { get; init; }

    public string? Output { get; init; }

    public int? TimeoutMs { get; init; }

    public int? RetryCount { get; init; }

    public Dictionary<string, JsonElement>? Parameters { get; init; }

    public List<ScenarioStep> Children { get; init; } = [];
}

public sealed class ScenarioVariableBag
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> Values => _values;

    public void Set(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _values[name] = value;
    }

    public bool TryGetValue(string name, out string value) => _values.TryGetValue(name, out value!);
}

public sealed record StepExecutionResult
{
    public required int Index { get; init; }

    public required StepType Type { get; init; }

    public bool Success { get; init; }

    public string? OutputName { get; init; }

    public string? OutputValue { get; init; }

    public string? ArtifactPath { get; init; }

    public string? ErrorMessage { get; init; }
}

public sealed record ScenarioExecutionResult
{
    public string ScenarioName { get; init; } = string.Empty;

    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public List<StepExecutionResult> Steps { get; init; } = [];
}
