using FluentAssertions;
using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application.Tests;

public sealed class ScenarioValidationServiceTests
{
    [Fact]
    public void Validate_WhenScenarioIsValid_ReturnsValidResult()
    {
        var service = new ScenarioValidationService();

        var result = service.Validate(new ScenarioDefinition
        {
            Name = "Smoke scenario",
            Steps =
            [
                new ScenarioStep
                {
                    Type = StepType.OpenUrl,
                    Url = "https://example.com",
                },
            ],
        });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}
