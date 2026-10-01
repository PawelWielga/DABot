using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopAutomationBot.Core;

public static class ScenarioSchema
{
    public const int CurrentVersion = 1;
}

public sealed record ScenarioDefinition
{
    public int SchemaVersion { get; init; } = ScenarioSchema.CurrentVersion;

    public string Name { get; init; } = string.Empty;

    public int? TimeoutMs { get; init; }

    public string? BrowserProfile { get; init; }

    public List<ScenarioStep> Steps { get; init; } = [];
}

public sealed record ScenarioStep
{
    public string? Id { get; init; }

    public StepType Type { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Enabled { get; init; }

    [JsonIgnore]
    public bool IsEnabled => Enabled is not false;

    public string? Selector { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ScenarioLocator? Locator { get; init; }

    public string? Url { get; init; }

    public string? Value { get; init; }

    public string? Output { get; init; }

    public int? TimeoutMs { get; init; }

    public int? RetryCount { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RetryDelayMs { get; init; }

    public StepRetrySafety? RetrySafety { get; init; }

    public Dictionary<string, JsonElement>? Parameters { get; init; }

    public List<ScenarioStep> Children { get; init; } = [];
}

public sealed class ScenarioVariableBag
{
    private readonly Dictionary<string, ScenarioVariableValue> _values =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, ScenarioVariableValue> Values => _values;

    public void Set(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        _values[name] = ScenarioVariableValue.FromString(value);
    }

    public void Set(string name, ScenarioVariableValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        _values[name] = value;
    }

    public bool TryGetValue(
        string name,
        out ScenarioVariableValue value) =>
        _values.TryGetValue(name, out value!);
}

public sealed record StepExecutionResult
{
    public required int Index { get; init; }

    public required StepType Type { get; init; }

    public bool Success { get; init; }

    public string? OutputName { get; init; }

    public string? OutputValue { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ScenarioVariableValue? OutputVariableValue { get; init; }

    public string? ArtifactPath { get; init; }

    public string? ErrorMessage { get; init; }
}

public sealed record ScenarioExecutionResult
{
    public string ScenarioName { get; init; } = string.Empty;

    public bool Success { get; set; }

    public string? RunId { get; set; }

    public string? ErrorMessage { get; set; }

    public string? FailureScreenshotPath { get; set; }

    public string? FailureHtmlPath { get; set; }

    public string? ReportPath { get; set; }

    public List<StepExecutionResult> Steps { get; init; } = [];
}
