using System.Text.Json;
using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Core.Tests;

public sealed class ScenarioCompilerTests
{
    [Fact]
    public void Compile_NormalizesIdsAndProducesTypedNestedSteps()
    {
        var scenario = new ScenarioDefinition
        {
            Name = "compiled",
            Steps =
            [
                new ScenarioStep
                {
                    Type = StepType.If,
                    Value = "true",
                    Children =
                    [
                        new ScenarioStep
                        {
                            Type = StepType.Loop,
                            Value = "2",
                            Children =
                            [
                                new ScenarioStep
                                {
                                    Type = StepType.Click,
                                    Selector = "#save",
                                    RetryCount = 2,
                                    RetryDelayMs = 25,
                                },
                            ],
                        },
                    ],
                },
            ],
        };

        var compiled = ScenarioCompiler.Compile(scenario);

        compiled.Steps.Should().ContainSingle();
        var conditional = compiled.Steps[0].Should().BeOfType<CompiledIfStep>().Subject;
        conditional.Id.Should().Be("step-001");
        conditional.Condition.Should().Be("true");

        var loop = conditional.Children[0].Should().BeOfType<CompiledLoopStep>().Subject;
        loop.Id.Should().Be("step-001-001");
        loop.Count.Should().Be("2");

        var click = loop.Children[0].Should().BeOfType<CompiledActionStep>().Subject;
        click.Id.Should().Be("step-001-001-001");
        click.Selector.Should().Be("#save");
        click.RetryCount.Should().Be(2);
        click.RetryDelayMs.Should().Be(25);
    }

    [Fact]
    public void Compile_WhenScenarioIsInvalid_ThrowsBeforeExecution()
    {
        var scenario = new ScenarioDefinition
        {
            Name = "invalid",
            Steps =
            [
                new ScenarioStep
                {
                    Type = StepType.Loop,
                    Value = "-1",
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

        var action = () => ScenarioCompiler.Compile(scenario);

        action.Should()
            .Throw<ScenarioValidationException>()
            .Which.Errors.Should()
            .Contain("scenario.steps[0].value must be a non-negative integer or a variable reference for Loop.");
    }


    [Fact]
    public void Compile_ActionLocator_RoundTripsWithoutFallingBackToSelector()
    {
        var scenario = new ScenarioDefinition
        {
            Name = "Locator compilation",
            Steps =
            [
                new ScenarioStep
                {
                    Type = StepType.Click,
                    Locator = new ScenarioLocator
                    {
                        Kind = ScenarioLocatorKind.TestId,
                        Value = "save-button",
                    },
                },
            ],
        };

        var compiled = ScenarioCompiler.Compile(scenario);
        var action = compiled.Steps[0]
            .Should().BeOfType<CompiledActionStep>().Subject;

        action.Selector.Should().BeNull();
        action.Locator.Should().NotBeNull();
        action.Locator!.Kind.Should().Be(ScenarioLocatorKind.TestId);

        var materialized = ScenarioCompiler.Materialize(compiled);
        materialized.Steps[0].Selector.Should().BeNull();
        materialized.Steps[0].Locator!.Value.Should().Be("save-button");
    }

    [Fact]
    public void Materialize_RoundTripsCompiledScenarioWithoutLosingParameters()
    {
        var scenario = new ScenarioDefinition
        {
            Name = "round-trip",
            TimeoutMs = 5000,
            Steps =
            [
                new ScenarioStep
                {
                    Id = "api",
                    Type = StepType.CallApi,
                    Url = "https://example.test",
                    Output = "result",
                    Parameters = new Dictionary<string, JsonElement>
                    {
                        ["method"] = JsonSerializer.SerializeToElement("POST"),
                    },
                },
            ],
        };

        var compiled = ScenarioCompiler.Compile(scenario);
        var materialized = ScenarioCompiler.Materialize(compiled);

        materialized.Name.Should().Be("round-trip");
        materialized.TimeoutMs.Should().Be(5000);
        materialized.Steps[0].Id.Should().Be("api");
        materialized.Steps[0].Url.Should().Be("https://example.test");
        materialized.Steps[0].Output.Should().Be("result");
        materialized.Steps[0].Parameters!["method"].GetString().Should().Be("POST");
    }

    [Fact]
    public void ScenarioVersion_Compile_UsesStoredImmutableDefinition()
    {
        var definition = new ScenarioDefinition
        {
            Name = "versioned",
            Steps =
            [
                new ScenarioStep
                {
                    Type = StepType.Screenshot,
                },
            ],
        };

        var version = ScenarioVersion.Capture(
            Guid.NewGuid(),
            1,
            definition,
            DateTimeOffset.UtcNow);

        definition.Steps.Clear();

        var compiled = version.Compile();

        compiled.Steps.Should().ContainSingle();
        compiled.Steps[0].Id.Should().Be("step-001");
        compiled.Steps[0].Should().BeOfType<CompiledActionStep>();
    }
}
