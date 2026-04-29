using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public sealed class ScenarioValidationService : IScenarioValidationService
{
    private readonly ScenarioDefinitionValidator _validator = new();

    public ScenarioValidationResult Validate(ScenarioDefinition scenario) => _validator.Validate(scenario);

    public void ValidateOrThrow(ScenarioDefinition scenario) => _validator.ValidateOrThrow(scenario);
}
