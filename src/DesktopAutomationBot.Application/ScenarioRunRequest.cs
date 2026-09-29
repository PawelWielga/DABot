using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public sealed record ScenarioRunRequest
{
    public required ScenarioVersion ScenarioVersion { get; init; }

    public Guid? RunId { get; init; }

    public ExecutionCursor? Cursor { get; init; }

    public Dictionary<string, string> Variables { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, ScenarioVariableValue> StructuredVariables { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public AutomationRun CreateRun(DateTimeOffset createdAt)
    {
        var variables = Variables.ToDictionary(
            pair => pair.Key,
            pair => ScenarioVariableValue.FromString(pair.Value),
            StringComparer.OrdinalIgnoreCase);

        foreach (var (name, value) in StructuredVariables)
        {
            variables[name] = value;
        }

        return AutomationRun.CreateStructured(
            ScenarioVersion,
            createdAt,
            RunId,
            Cursor,
            variables);
    }
}
