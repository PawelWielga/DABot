using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Core.Tests;

public sealed class StepAttemptTests
{
    [Theory]
    [InlineData(StepType.ReadText, StepRetrySafety.SafeToRetry)]
    [InlineData(StepType.WaitFor, StepRetrySafety.SafeToRetry)]
    [InlineData(StepType.Screenshot, StepRetrySafety.SafeToRetry)]
    [InlineData(StepType.Delay, StepRetrySafety.SafeToRetry)]
    [InlineData(StepType.If, StepRetrySafety.SafeToRetry)]
    [InlineData(StepType.Loop, StepRetrySafety.SafeToRetry)]
    [InlineData(StepType.OpenUrl, StepRetrySafety.Idempotent)]
    [InlineData(StepType.FillText, StepRetrySafety.Idempotent)]
    [InlineData(StepType.PasteText, StepRetrySafety.Idempotent)]
    [InlineData(StepType.Click, StepRetrySafety.NeedsVerification)]
    [InlineData(StepType.CallApi, StepRetrySafety.NeverRetryAutomatically)]
    public void GetDefault_ReturnsConservativeRetrySafety(
        StepType stepType,
        StepRetrySafety expected)
    {
        StepRetrySafetyClassifier.GetDefault(stepType).Should().Be(expected);
    }

    [Fact]
    public void Start_UsesExplicitRetrySafetyOverride()
    {
        var startedAt = DateTimeOffset.Parse("2026-09-27T10:00:00+02:00");
        var step = new ScenarioStep
        {
            Id = "submit",
            Type = StepType.Click,
            RetrySafety = StepRetrySafety.NeverRetryAutomatically,
        };

        var attempt = StepAttempt.Start(Guid.NewGuid(), step, 1, startedAt);

        attempt.Status.Should().Be(StepAttemptStatus.Started);
        attempt.StepId.Should().Be("submit");
        attempt.AttemptNumber.Should().Be(1);
        attempt.RetrySafety.Should().Be(StepRetrySafety.NeverRetryAutomatically);
        attempt.StartedAt.Should().Be(startedAt);
        attempt.UpdatedAt.Should().Be(startedAt);
        attempt.FinishedAt.Should().BeNull();
    }

    [Fact]
    public void MarkUnknown_RepresentsCrashAfterAttemptStarted()
    {
        var startedAt = DateTimeOffset.Parse("2026-09-27T10:00:00+02:00");
        var detectedAt = startedAt.AddSeconds(30);
        var attempt = StepAttempt.Start(
            Guid.NewGuid(),
            new ScenarioStep
            {
                Id = "submit",
                Type = StepType.Click,
            },
            1,
            startedAt);

        var interrupted = attempt.MarkUnknown(detectedAt);

        interrupted.Status.Should().Be(StepAttemptStatus.Unknown);
        interrupted.UpdatedAt.Should().Be(detectedAt);
        interrupted.FinishedAt.Should().BeNull();
    }

    [Fact]
    public void Decide_WhenInterruptedClick_RequiresVerification()
    {
        var attempt = CreateInterruptedAttempt(StepType.Click);

        StepAttemptRecoveryPolicy.Decide(attempt)
            .Should().Be(StepRecoveryAction.VerifyBeforeRetry);
    }

    [Fact]
    public void Decide_WhenInterruptedCallApi_WaitsForHuman()
    {
        var attempt = CreateInterruptedAttempt(StepType.CallApi);

        StepAttemptRecoveryPolicy.Decide(attempt)
            .Should().Be(StepRecoveryAction.WaitingForHuman);
    }

    [Theory]
    [InlineData(StepType.ReadText)]
    [InlineData(StepType.FillText)]
    [InlineData(StepType.PasteText)]
    public void Decide_WhenInterruptedStepIsSafeOrIdempotent_RetriesAutomatically(
        StepType stepType)
    {
        var attempt = CreateInterruptedAttempt(stepType);

        StepAttemptRecoveryPolicy.Decide(attempt)
            .Should().Be(StepRecoveryAction.RetryAutomatically);
    }

    [Fact]
    public void Decide_WhenCompletedAttempt_ReturnsNone()
    {
        var startedAt = DateTimeOffset.Parse("2026-09-27T10:00:00+02:00");
        var attempt = StepAttempt.Start(
                Guid.NewGuid(),
                new ScenarioStep
                {
                    Id = "read",
                    Type = StepType.ReadText,
                },
                1,
                startedAt)
            .MarkCompleted(startedAt.AddSeconds(1));

        StepAttemptRecoveryPolicy.Decide(attempt)
            .Should().Be(StepRecoveryAction.None);
    }

    [Fact]
    public void MarkFailed_PreservesFailureForDiagnostics()
    {
        var startedAt = DateTimeOffset.Parse("2026-09-27T10:00:00+02:00");
        var failedAt = startedAt.AddSeconds(2);
        var attempt = StepAttempt.Start(
                Guid.NewGuid(),
                new ScenarioStep
                {
                    Id = "read",
                    Type = StepType.ReadText,
                },
                2,
                startedAt)
            .MarkFailed("selector not found", failedAt);

        attempt.Status.Should().Be(StepAttemptStatus.Failed);
        attempt.ErrorMessage.Should().Be("selector not found");
        attempt.FinishedAt.Should().Be(failedAt);
        attempt.AttemptNumber.Should().Be(2);
    }

    [Fact]
    public void MarkUnknown_WhenAttemptIsAlreadyCompleted_Throws()
    {
        var startedAt = DateTimeOffset.Parse("2026-09-27T10:00:00+02:00");
        var completed = StepAttempt.Start(
                Guid.NewGuid(),
                new ScenarioStep
                {
                    Id = "read",
                    Type = StepType.ReadText,
                },
                1,
                startedAt)
            .MarkCompleted(startedAt.AddSeconds(1));

        var act = () => completed.MarkUnknown(startedAt.AddSeconds(2));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Restore_RehydratesPersistedCompletedAttempt()
    {
        var attemptId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var startedAt = DateTimeOffset.Parse("2026-09-27T10:00:00+02:00");
        var completedAt = startedAt.AddSeconds(3);

        var restored = StepAttempt.Restore(
            attemptId,
            runId,
            "read",
            StepType.ReadText,
            2,
            StepRetrySafety.SafeToRetry,
            StepAttemptStatus.Completed,
            startedAt,
            completedAt,
            completedAt,
            errorMessage: null);

        restored.AttemptId.Should().Be(attemptId);
        restored.RunId.Should().Be(runId);
        restored.StepId.Should().Be("read");
        restored.AttemptNumber.Should().Be(2);
        restored.Status.Should().Be(StepAttemptStatus.Completed);
        restored.FinishedAt.Should().Be(completedAt);
    }

    [Fact]
    public void Restore_WhenPersistedStateShapeIsInvalid_Throws()
    {
        var startedAt = DateTimeOffset.Parse("2026-09-27T10:00:00+02:00");

        var act = () => StepAttempt.Restore(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "read",
            StepType.ReadText,
            1,
            StepRetrySafety.SafeToRetry,
            StepAttemptStatus.Failed,
            startedAt,
            startedAt.AddSeconds(1),
            finishedAt: null,
            errorMessage: "failed");

        act.Should().Throw<ArgumentException>();
    }

    private static StepAttempt CreateInterruptedAttempt(StepType stepType)
    {
        var startedAt = DateTimeOffset.Parse("2026-09-27T10:00:00+02:00");

        return StepAttempt.Start(
                Guid.NewGuid(),
                new ScenarioStep
                {
                    Id = "step",
                    Type = stepType,
                },
                1,
                startedAt)
            .MarkUnknown(startedAt.AddSeconds(10));
    }
}
