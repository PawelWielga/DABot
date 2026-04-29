using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Core.Tests;

public sealed class ScenarioDefinitionValidatorTests
{
    [Fact]
    public void Validate_WhenScenarioHasNoSteps_ReturnsValidationError()
    {
        var validator = new ScenarioDefinitionValidator();

        var result = validator.Validate(new ScenarioDefinition
        {
            Name = "Empty scenario",
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("scenario.steps must contain at least one step.");
    }

    [Fact]
    public void Validate_WhenOpenUrlIsMissingUrl_ReturnsValidationError()
    {
        var validator = new ScenarioDefinitionValidator();

        var result = validator.Validate(new ScenarioDefinition
        {
            Name = "Broken scenario",
            Steps =
            [
                new ScenarioStep
                {
                    Type = StepType.OpenUrl,
                },
            ],
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("scenario.steps[0].url is required for OpenUrl.");
    }

    [Fact]
    public void Validate_WhenReadTextIsMissingOutput_ReturnsValidationError()
    {
        var validator = new ScenarioDefinitionValidator();

        var result = validator.Validate(new ScenarioDefinition
        {
            Name = "Broken scenario",
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
