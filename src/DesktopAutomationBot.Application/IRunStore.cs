using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public sealed record StoredAutomationRun(
    AutomationRun Run,
    ScenarioVersion ScenarioVersion);

public interface IRunStore
{
    Task SaveAsync(
        AutomationRun run,
        ScenarioVersion scenarioVersion,
        CancellationToken cancellationToken = default);

    Task<StoredAutomationRun?> LoadAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
}
