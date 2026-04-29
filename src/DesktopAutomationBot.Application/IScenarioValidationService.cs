using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public interface IScenarioValidationService
{
    ScenarioValidationResult Validate(ScenarioDefinition scenario);

    void ValidateOrThrow(ScenarioDefinition scenario);
}
