using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public sealed record ScenarioRunRequest
{
    public required ScenarioVersion ScenarioVersion { get; init; }

    public Guid? RunId { get; init; }

    public ExecutionCursor? Cursor { get; init; }

    public Dictionary<string, string> Variables { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public AutomationRun CreateRun(DateTimeOffset createdAt) =>
        AutomationRun.Create(
            ScenarioVersion,
            createdAt,
            RunId,
            Cursor,
            Variables);
}
