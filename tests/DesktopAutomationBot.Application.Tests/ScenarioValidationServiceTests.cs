using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using FluentAssertions;

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

    [Fact]
    public void Validate_WhenReadTextIsMissingOutput_ReturnsValidationError()
    {
        var service = new ScenarioValidationService();

        var result = service.Validate(new ScenarioDefinition
        {
            Name = "Broken read text scenario",
            Steps =
            [
                new ScenarioStep
                {
                    Type = StepType.ReadText,
                    Selector = ".result",
                },
            ],
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("scenario.steps[0].output is required for ReadText.");
    }
}
