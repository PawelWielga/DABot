using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;

var validator = new ScenarioValidationService();
var sampleScenario = new ScenarioDefinition
{
    Name = "Sprint 0 smoke check",
    Steps =
    [
        new ScenarioStep
        {
            Type = StepType.OpenUrl,
            Url = "https://example.com",
        },
    ],
};

validator.ValidateOrThrow(sampleScenario);
Console.WriteLine($"Scenario '{sampleScenario.Name}' is valid.");
