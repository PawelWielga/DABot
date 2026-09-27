using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Core.Tests;

public sealed class AutomationRunTests
{
    [Fact]
    public void Create_BindsRunToImmutableScenarioVersion()
    {
        var scenarioVersion = CreateVersion();
        var createdAt = DateTimeOffset.Parse("2026-09-27T12:00:00+02:00");

        var run = AutomationRun.Create(scenarioVersion, createdAt);

        run.RunId.Should().NotBe(Guid.Empty);
        run.ScenarioId.Should().Be(scenarioVersion.ScenarioId);
        run.ScenarioVersionId.Should().Be(scenarioVersion.VersionId);
        run.State.Status.Should().Be(RunStatus.Queued);
        run.State.WaitReason.Should().BeNull();
        run.Cursor.NextStepId.Should().Be("open");
        run.CreatedAt.Should().Be(createdAt);
        run.UpdatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void Create_WhenRunIdIsSupplied_PreservesExternalIdentity()
    {
        var runId = Guid.NewGuid();

        var run = AutomationRun.Create(
            CreateVersion(),
            DateTimeOffset.UtcNow,
            runId);

        run.RunId.Should().Be(runId);
    }

    [Fact]
    public void Create_CopiesInitialVariablesCaseInsensitively()
    {
        var variables = new Dictionary<string, string>
        {
            ["Token"] = "first",
        };

        var run = AutomationRun.Create(
            CreateVersion(),
            DateTimeOffset.UtcNow,
            variables: variables);

        variables["Token"] = "changed";

        run.Variables["token"].Should().Be("first");
    }

    [Fact]
    public void Create_CopiesExecutionCursor()
    {
        var version = CreateVersion();
        var cursor = ExecutionCursor.Start(version.MaterializeDefinition());

        var run = AutomationRun.Create(
            version,
            DateTimeOffset.UtcNow,
            cursor: cursor);

        cursor.Frames.Add(new ExecutionFrame
        {
            StepId = "open",
            Kind = ExecutionFrameKind.If,
            NextChildIndex = 0,
        });

        run.Cursor.Frames.Should().BeEmpty();
    }

    [Fact]
    public void Create_WhenExternalRunIdIsEmpty_Throws()
    {
        var act = () => AutomationRun.Create(
            CreateVersion(),
            DateTimeOffset.UtcNow,
            Guid.Empty);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Run ID*empty*");
    }

    [Fact]
    public void Create_WhenCursorBelongsToDifferentScenarioVersion_Throws()
    {
        var version = CreateVersion();
        var foreignVersion = ScenarioVersion.Capture(
            Guid.NewGuid(),
            1,
            new ScenarioDefinition
            {
                Name = "Other",
                Steps =
                [
                    new ScenarioStep
                    {
                        Id = "other-step",
                        Type = StepType.Screenshot,
                    },
                ],
            },
            DateTimeOffset.UtcNow);

        var foreignCursor = ExecutionCursor.Start(
            foreignVersion.MaterializeDefinition());

        var act = () => AutomationRun.Create(
            version,
            DateTimeOffset.UtcNow,
            cursor: foreignCursor);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*not valid for the scenario version*");
    }

    [Fact]
    public void Create_WhenCursorIsCompleted_Throws()
    {
        var act = () => AutomationRun.Create(
            CreateVersion(),
            DateTimeOffset.UtcNow,
            cursor: ExecutionCursor.Completed());

        act.Should().Throw<ArgumentException>()
            .WithMessage("*queued run*completed execution cursor*");
    }

    [Fact]
    public void Restore_PreservesPersistedStateAndTimestamps()
    {
        var version = CreateVersion();
        var createdAt = DateTimeOffset.Parse("2026-09-27T12:00:00+02:00");
        var updatedAt = createdAt.AddMinutes(5);
        var state = RunState.Restore(
            RunStatus.Waiting,
            RunWaitReason.Event);
        var cursor = ExecutionCursor.Start(version.MaterializeDefinition());

        var run = AutomationRun.Restore(
            Guid.NewGuid(),
            version,
            state,
            cursor,
            new Dictionary<string, string> { ["value"] = "42" },
            createdAt,
            updatedAt);

        run.State.Should().Be(state);
        run.Cursor.Should().BeEquivalentTo(cursor);
        run.Variables["value"].Should().Be("42");
        run.CreatedAt.Should().Be(createdAt);
        run.UpdatedAt.Should().Be(updatedAt);
        run.ScenarioVersionId.Should().Be(version.VersionId);
    }

    [Fact]
    public void Restore_WhenUpdatedAtPredatesCreatedAt_Throws()
    {
        var version = CreateVersion();
        var createdAt = DateTimeOffset.UtcNow;

        var act = () => AutomationRun.Restore(
            Guid.NewGuid(),
            version,
            RunState.CreateQueued(),
            ExecutionCursor.Start(version.MaterializeDefinition()),
            variables: null,
            createdAt,
            createdAt.AddSeconds(-1));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*updated timestamp*");
    }

    [Fact]
    public void Restore_WhenCompletedStateHasIncompleteCursor_Throws()
    {
        var version = CreateVersion();

        var act = () => AutomationRun.Restore(
            Guid.NewGuid(),
            version,
            RunState.Restore(RunStatus.Completed),
            ExecutionCursor.Start(version.MaterializeDefinition()),
            variables: null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*completed run*completed execution cursor*");
    }

    [Fact]
    public void Restore_WhenCompletedStateHasCompletedCursor_Succeeds()
    {
        var version = CreateVersion();

        var run = AutomationRun.Restore(
            Guid.NewGuid(),
            version,
            RunState.Restore(RunStatus.Completed),
            ExecutionCursor.Completed(),
            variables: null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        run.State.IsTerminal.Should().BeTrue();
        run.Cursor.IsCompleted.Should().BeTrue();
    }

    private static ScenarioVersion CreateVersion() =>
        ScenarioVersion.Capture(
            Guid.NewGuid(),
            1,
            new ScenarioDefinition
            {
                Name = "Scenario",
                Steps =
                [
                    new ScenarioStep
                    {
                        Id = "open",
                        Type = StepType.OpenUrl,
                        Url = "https://example.com",
                    },
                    new ScenarioStep
                    {
                        Id = "done",
                        Type = StepType.Screenshot,
                    },
                ],
            },
            DateTimeOffset.Parse("2026-09-27T11:00:00+02:00"));
}
