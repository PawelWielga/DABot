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

    [Fact]
    public void Validate_WhenSchemaVersionIsUnsupported_ReturnsValidationError()
    {
        var validator = new ScenarioDefinitionValidator();

        var result = validator.Validate(new ScenarioDefinition
        {
            SchemaVersion = ScenarioSchema.CurrentVersion + 1,
            Name = "Future scenario",
            Steps =
            [
                new ScenarioStep
                {
                    Type = StepType.OpenUrl,
                    Url = "https://example.com",
                },
            ],
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(
            $"scenario.schemaVersion '{ScenarioSchema.CurrentVersion + 1}' is not supported. Supported version is {ScenarioSchema.CurrentVersion}.");
    }

    [Fact]
    public void Validate_WhenNestedStepIdsAreDuplicated_ReturnsValidationError()
    {
        var validator = new ScenarioDefinitionValidator();

        var result = validator.Validate(new ScenarioDefinition
        {
            Name = "Duplicate IDs",
            Steps =
            [
                new ScenarioStep
                {
                    Id = "same-step",
                    Type = StepType.OpenUrl,
                    Url = "https://example.com",
                },
                new ScenarioStep
                {
                    Type = StepType.Loop,
                    Children =
                    [
                        new ScenarioStep
                        {
                            Id = "SAME-STEP",
                            Type = StepType.Screenshot,
                        },
                    ],
                },
            ],
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(
            "scenario.steps[1].children[0].id 'SAME-STEP' duplicates scenario.steps[0].id.");
    }

    [Fact]
    public void Normalize_WhenIdsAreMissing_AssignsDeterministicIdsAndPreservesExplicitIds()
    {
        var scenario = new ScenarioDefinition
        {
            Name = "Legacy scenario",
            Steps =
            [
                new ScenarioStep
                {
                    Type = StepType.OpenUrl,
                    Url = "https://example.com",
                },
                new ScenarioStep
                {
                    Id = "custom-loop",
                    Type = StepType.Loop,
                    Children =
                    [
                        new ScenarioStep
                        {
                            Type = StepType.Screenshot,
                        },
                    ],
                },
            ],
        };

        var normalized = ScenarioDefinitionNormalizer.Normalize(scenario);

        normalized.SchemaVersion.Should().Be(ScenarioSchema.CurrentVersion);
        normalized.Steps[0].Id.Should().Be("step-001");
        normalized.Steps[1].Id.Should().Be("custom-loop");
        normalized.Steps[1].Children[0].Id.Should().Be("step-002-001");
    }

    [Fact]
    public void Normalize_WhenGeneratedIdCollidesWithExplicitId_UsesDeterministicSuffix()
    {
        var scenario = new ScenarioDefinition
        {
            Name = "Collision scenario",
            Steps =
            [
                new ScenarioStep
                {
                    Type = StepType.Screenshot,
                },
                new ScenarioStep
                {
                    Id = "step-001",
                    Type = StepType.Screenshot,
                },
            ],
        };

        var normalized = ScenarioDefinitionNormalizer.Normalize(scenario);

        normalized.Steps[0].Id.Should().Be("step-001-2");
        normalized.Steps[1].Id.Should().Be("step-001");
    }
    [Fact]
    public void Validate_WhenRetryCountIsNegative_ReturnsValidationError()
    {
        var validator = new ScenarioDefinitionValidator();

        var result = validator.Validate(new ScenarioDefinition
        {
            Name = "Invalid retry count",
            Steps =
            [
                new ScenarioStep
                {
                    Type = StepType.OpenUrl,
                    Url = "https://example.com",
                    RetryCount = -1,
                },
            ],
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(
            "scenario.steps[0].retryCount must be zero or greater.");
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    public void StepRetryPolicy_MaximumAttempts_IsInitialAttemptPlusRetries(
        int? retryCount,
        int expectedMaximumAttempts)
    {
        var step = new ScenarioStep
        {
            Type = StepType.Screenshot,
            RetryCount = retryCount,
        };

        StepRetryPolicy.GetMaximumAttempts(step)
            .Should()
            .Be(expectedMaximumAttempts);
    }

    [Fact]
    public void StepRetryPolicy_HasRemainingAttempt_UsesHighestPersistedAttemptNumber()
    {
        var step = new ScenarioStep
        {
            Type = StepType.Screenshot,
            RetryCount = 2,
        };

        StepRetryPolicy.HasRemainingAttempt(step, 2).Should().BeTrue();
        StepRetryPolicy.HasRemainingAttempt(step, 3).Should().BeFalse();
    }
}
