using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public interface IScenarioLoader
{
    Task<ScenarioDefinition> LoadAsync(string scenarioPath, CancellationToken cancellationToken = default);
}
