using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class ScenarioHandlerValidatorTests
{
    [Fact]
    public void Validate_WhenEveryDeclaredTypeIsRegistered_ReturnsValidResult()
    {
        var scenario = new ScenarioDefinition
        {
            Name = "supported",
            Steps =
            [
                new ScenarioStep
                {
                    Type = StepType.If,
                    Children =
                    [
                        new ScenarioStep
                        {
                            Type = StepType.OpenUrl,
                            Url = "https://example.com",
                        },
                    ],
                },
            ],
        };

        var result = ScenarioHandlerValidator.Validate(
            scenario,
            [StepType.If, StepType.OpenUrl]);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WhenNestedStepHasNoHandler_ReturnsPathSpecificError()
    {
        var scenario = new ScenarioDefinition
        {
            Name = "unsupported nested step",
            Steps =
            [
                new ScenarioStep
                {
                    Type = StepType.If,
                    Children =
                    [
                        new ScenarioStep
                        {
                            Type = StepType.CallApi,
                        },
                    ],
                },
            ],
        };

        var result = ScenarioHandlerValidator.Validate(
            scenario,
            [StepType.If]);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(
            "scenario.steps[0].children[0].type 'CallApi' has no registered handler.");
    }

    [Fact]
    public void ValidateOrThrow_WhenControlFlowStepHasNoHandler_DoesNotRequireHandler()
    {
        var scenario = new ScenarioDefinition
        {
            Name = "unsupported",
            Steps =
            [
                new ScenarioStep
                {
                    Type = StepType.Loop,
                    Children =
                    [
                        new ScenarioStep
                        {
                            Type = StepType.OpenUrl,
                            Url = "https://example.com",
                        },
                    ],
                },
            ],
        };

        Action action = () => ScenarioHandlerValidator.ValidateOrThrow(
            scenario,
            [StepType.OpenUrl]);

        action.Should().NotThrow();
    }
}
