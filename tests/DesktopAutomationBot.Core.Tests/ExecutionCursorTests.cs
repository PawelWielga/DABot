using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Core.Tests;

public sealed class ExecutionCursorTests
{
    [Fact]
    public void Start_WhenScenarioHasSteps_PointsAtFirstNormalizedStep()
    {
        var scenario = new ScenarioDefinition
        {
            Name = "Start",
            Steps =
            [
                new ScenarioStep
                {
                    Type = StepType.OpenUrl,
                    Url = "https://example.com",
                },
            ],
        };

        var cursor = ExecutionCursor.Start(scenario);

        cursor.Version.Should().Be(ExecutionCursorSchema.CurrentVersion);
        cursor.NextStepId.Should().Be("step-001");
        cursor.Frames.Should().BeEmpty();
        cursor.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void Completed_HasNoNextStepOrFrames()
    {
        var cursor = ExecutionCursor.Completed();

        cursor.NextStepId.Should().BeNull();
        cursor.Frames.Should().BeEmpty();
        cursor.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenCursorTargetsNestedLoopStep_AcceptsMatchingFrameStack()
    {
        var scenario = CreateNestedScenario();
        var validator = new ExecutionCursorValidator();
        var cursor = new ExecutionCursor
        {
            NextStepId = "read-value",
            Frames =
            [
                new ExecutionFrame
                {
                    StepId = "outer-loop",
                    Kind = ExecutionFrameKind.Loop,
                    NextChildIndex = 0,
                    Iteration = 3,
                },
                new ExecutionFrame
                {
                    StepId = "inner-if",
                    Kind = ExecutionFrameKind.If,
                    NextChildIndex = 0,
                },
            ],
        };

        var result = validator.Validate(scenario, cursor);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WhenFrameDoesNotMatchNestedPath_ReturnsValidationError()
    {
        var scenario = CreateNestedScenario();
        var validator = new ExecutionCursorValidator();
        var cursor = new ExecutionCursor
        {
            NextStepId = "read-value",
            Frames =
            [
                new ExecutionFrame
                {
                    StepId = "outer-loop",
                    Kind = ExecutionFrameKind.Loop,
                    NextChildIndex = 1,
                    Iteration = 0,
                },
                new ExecutionFrame
                {
                    StepId = "inner-if",
                    Kind = ExecutionFrameKind.If,
                    NextChildIndex = 0,
                },
            ],
        };

        var result = validator.Validate(scenario, cursor);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(
            "cursor.frames[0].nextChildIndex '1' does not match the child path index '0'.");
    }

    [Fact]
    public void Validate_WhenLoopIterationIsMissing_ReturnsValidationError()
    {
        var scenario = CreateNestedScenario();
        var validator = new ExecutionCursorValidator();
        var cursor = new ExecutionCursor
        {
            NextStepId = "inner-if",
            Frames =
            [
                new ExecutionFrame
                {
                    StepId = "outer-loop",
                    Kind = ExecutionFrameKind.Loop,
                    NextChildIndex = 0,
                },
            ],
        };

        var result = validator.Validate(scenario, cursor);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(
            "cursor.frames[0].iteration must be zero or greater for Loop frames.");
    }

    [Fact]
    public void Validate_WhenCompletedCursorHasFrames_ReturnsValidationError()
    {
        var scenario = CreateNestedScenario();
        var validator = new ExecutionCursorValidator();
        var cursor = new ExecutionCursor
        {
            Frames =
            [
                new ExecutionFrame
                {
                    StepId = "outer-loop",
                    Kind = ExecutionFrameKind.Loop,
                    NextChildIndex = 0,
                    Iteration = 0,
                },
            ],
        };

        var result = validator.Validate(scenario, cursor);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("cursor.frames must be empty when cursor.nextStepId is null.");
    }

    [Fact]
    public void Json_RoundTrip_PreservesNestedCursorState()
    {
        var cursor = new ExecutionCursor
        {
            NextStepId = "read-value",
            Frames =
            [
                new ExecutionFrame
                {
                    StepId = "outer-loop",
                    Kind = ExecutionFrameKind.Loop,
                    NextChildIndex = 0,
                    Iteration = 7,
                },
                new ExecutionFrame
                {
                    StepId = "inner-if",
                    Kind = ExecutionFrameKind.If,
                    NextChildIndex = 0,
                },
            ],
        };

        var json = ExecutionCursorJson.Serialize(cursor);
        var restored = ExecutionCursorJson.Deserialize(json);

        restored.Should().BeEquivalentTo(cursor);
        json.Should().Contain("\"version\":1");
        json.Should().Contain("\"kind\":\"Loop\"");
    }

    private static ScenarioDefinition CreateNestedScenario() =>
        new()
        {
            Name = "Nested cursor",
            Steps =
            [
                new ScenarioStep
                {
                    Id = "outer-loop",
                    Type = StepType.Loop,
                    Children =
                    [
                        new ScenarioStep
                        {
                            Id = "inner-if",
                            Type = StepType.If,
                            Children =
                            [
                                new ScenarioStep
                                {
                                    Id = "read-value",
                                    Type = StepType.ReadText,
                                    Selector = ".value",
                                    Output = "value",
                                },
                            ],
                        },
                        new ScenarioStep
                        {
                            Id = "loop-screenshot",
                            Type = StepType.Screenshot,
                        },
                    ],
                },
                new ScenarioStep
                {
                    Id = "done",
                    Type = StepType.Screenshot,
                },
            ],
        };
}
