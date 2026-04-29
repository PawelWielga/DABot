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
