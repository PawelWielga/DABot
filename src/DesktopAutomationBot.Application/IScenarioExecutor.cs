using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public interface IScenarioExecutor
{
    Task<ScenarioExecutionResult> ExecuteAsync(ScenarioDefinition scenario, CancellationToken cancellationToken = default);
}
