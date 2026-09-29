using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class DurableRunRecoveryServiceTests
{
    [Fact]
    public async Task RecoverAsync_WhenIdempotentAttemptWasInterrupted_WaitsForAutomaticRetry()
    {
        var version = CreateVersion(
            new ScenarioStep
            {
                Id = "open",
                Type = StepType.OpenUrl,
                Url = "https://example.com",
                RetryCount = 1,
                RetryDelayMs = 30000,
            });
        var run = CreateRunningRun(version);
        var step = version.MaterializeDefinition().Steps[0];
        var started = StepAttempt.Start(
            run.RunId,
            step,
            1,
            run.UpdatedAt.AddSeconds(1));
        var runStore = new InMemoryRunStore(run, version);
        var attemptStore = new InMemoryStepAttemptStore(started);
        var service = CreateService(
            runStore,
            attemptStore,
            run.UpdatedAt.AddMinutes(1));

        var result = await service.RecoverAsync(run.RunId);

        result.Outcome.Should().Be(DurableRunRecoveryOutcome.AutomaticResume);
        result.Run.State.Status.Should().Be(RunStatus.Waiting);
        result.Run.State.WaitReason.Should().Be(RunWaitReason.Retry);
        result.Run.RetryNotBefore.Should().Be(
            result.Run.UpdatedAt.AddSeconds(30));
        result.Run.Cursor.NextStepId.Should().Be("open");

        attemptStore.Attempts.Should().ContainSingle();
        attemptStore.Attempts[0].Status.Should().Be(StepAttemptStatus.Unknown);

        result.Decisions.Should().ContainSingle();
        result.Decisions[0].Action.Should().Be(StepRecoveryAction.RetryAutomatically);
    }

    [Fact]
    public async Task RecoverAsync_WhenInterruptedSafeAttemptExhaustedRetryBudget_FailsRun()
    {
        var version = CreateVersion(
            new ScenarioStep
            {
                Id = "open",
                Type = StepType.OpenUrl,
                Url = "https://example.com",
                RetryCount = 0,
            });
        var run = CreateRunningRun(version);
        var started = StepAttempt.Start(
            run.RunId,
            version.MaterializeDefinition().Steps[0],
            1,
            run.UpdatedAt.AddSeconds(1));
        var runStore = new InMemoryRunStore(run, version);
        var attemptStore = new InMemoryStepAttemptStore(started);
        var service = CreateService(
            runStore,
            attemptStore,
            run.UpdatedAt.AddMinutes(1));

        var result = await service.RecoverAsync(run.RunId);

        result.Outcome.Should().Be(DurableRunRecoveryOutcome.Failed);
        result.Run.State.Status.Should().Be(RunStatus.Failed);
        attemptStore.Attempts.Should().ContainSingle();
        attemptStore.Attempts[0].Status.Should().Be(StepAttemptStatus.Unknown);
    }

    [Fact]
    public async Task RecoverAsync_WhenClickWasInterrupted_RequiresVerification()
    {
        var version = CreateVersion(
            new ScenarioStep
            {
                Id = "submit",
                Type = StepType.Click,
                Selector = "#submit",
            });
        var run = CreateRunningRun(version);
        var started = StepAttempt.Start(
            run.RunId,
            version.MaterializeDefinition().Steps[0],
            1,
            run.UpdatedAt.AddSeconds(1));
        var runStore = new InMemoryRunStore(run, version);
        var attemptStore = new InMemoryStepAttemptStore(started);
        var service = CreateService(
            runStore,
            attemptStore,
            run.UpdatedAt.AddMinutes(1));

        var result = await service.RecoverAsync(run.RunId);

        result.Outcome.Should().Be(DurableRunRecoveryOutcome.VerificationRequired);
        result.Run.State.Status.Should().Be(RunStatus.Waiting);
        result.Run.State.WaitReason.Should().Be(RunWaitReason.Human);
        result.Decisions.Should().ContainSingle();
        result.Decisions[0].Action.Should().Be(StepRecoveryAction.VerifyBeforeRetry);
    }

    [Fact]
    public async Task RecoverAsync_WhenCallApiWasInterrupted_RequiresHumanDecision()
    {
        var version = CreateVersion(
            new ScenarioStep
            {
                Id = "api",
                Type = StepType.CallApi,
            });
        var run = CreateRunningRun(version);
        var started = StepAttempt.Start(
            run.RunId,
            version.MaterializeDefinition().Steps[0],
            1,
            run.UpdatedAt.AddSeconds(1));
        var runStore = new InMemoryRunStore(run, version);
        var attemptStore = new InMemoryStepAttemptStore(started);
        var service = CreateService(
            runStore,
            attemptStore,
            run.UpdatedAt.AddMinutes(1));

        var result = await service.RecoverAsync(run.RunId);

        result.Outcome.Should().Be(DurableRunRecoveryOutcome.HumanDecisionRequired);
        result.Run.State.Status.Should().Be(RunStatus.Waiting);
        result.Run.State.WaitReason.Should().Be(RunWaitReason.Human);
        result.Decisions.Should().ContainSingle();
        result.Decisions[0].Action.Should().Be(StepRecoveryAction.WaitingForHuman);
    }

    [Fact]
    public async Task RecoverAsync_WhenCompletedAttemptWasPersistedBeforeCursor_AdvancesWithoutRepeatingStep()
    {
        var version = CreateVersion(
            new ScenarioStep
            {
                Id = "open",
                Type = StepType.OpenUrl,
                Url = "https://example.com",
            },
            new ScenarioStep
            {
                Id = "capture",
                Type = StepType.Screenshot,
            });
        var run = CreateRunningRun(version);
        var started = StepAttempt.Start(
            run.RunId,
            version.MaterializeDefinition().Steps[0],
            1,
            run.UpdatedAt.AddSeconds(1));
        var completed = started.MarkCompleted(
            started.StartedAt.AddSeconds(1));
        var runStore = new InMemoryRunStore(run, version);
        var attemptStore = new InMemoryStepAttemptStore(completed);
        var service = CreateService(
            runStore,
            attemptStore,
            run.UpdatedAt.AddMinutes(1));

        var result = await service.RecoverAsync(run.RunId);

        result.Outcome.Should().Be(DurableRunRecoveryOutcome.AutomaticResume);
        result.Run.State.Status.Should().Be(RunStatus.Waiting);
        result.Run.State.WaitReason.Should().Be(RunWaitReason.Retry);
        result.Run.Cursor.NextStepId.Should().Be("capture");
    }

    [Fact]
    public async Task RecoverAsync_WhenFinalStepCompletedBeforeRunSnapshot_CompletesRun()
    {
        var version = CreateVersion(
            new ScenarioStep
            {
                Id = "open",
                Type = StepType.OpenUrl,
                Url = "https://example.com",
            });
        var run = CreateRunningRun(version);
        var started = StepAttempt.Start(
            run.RunId,
            version.MaterializeDefinition().Steps[0],
            1,
            run.UpdatedAt.AddSeconds(1));
        var completed = started.MarkCompleted(
            started.StartedAt.AddSeconds(1));
        var runStore = new InMemoryRunStore(run, version);
        var attemptStore = new InMemoryStepAttemptStore(completed);
        var service = CreateService(
            runStore,
            attemptStore,
            run.UpdatedAt.AddMinutes(1));

        var result = await service.RecoverAsync(run.RunId);

        result.Outcome.Should().Be(DurableRunRecoveryOutcome.Completed);
        result.Run.State.Status.Should().Be(RunStatus.Completed);
        result.Run.Cursor.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task RecoverAsync_WhenCompletedReadOutputWasNotSnapshotted_RetriesToRebuildVariable()
    {
        var version = CreateVersion(
            new ScenarioStep
            {
                Id = "read",
                Type = StepType.ReadText,
                Selector = "#value",
                Output = "value",
                RetryCount = 1,
            });
        var run = CreateRunningRun(version);
        var started = StepAttempt.Start(
            run.RunId,
            version.MaterializeDefinition().Steps[0],
            1,
            run.UpdatedAt.AddSeconds(1));
        var completed = started.MarkCompleted(
            started.StartedAt.AddSeconds(1));
        var runStore = new InMemoryRunStore(run, version);
        var attemptStore = new InMemoryStepAttemptStore(completed);
        var service = CreateService(
            runStore,
            attemptStore,
            run.UpdatedAt.AddMinutes(1));

        var result = await service.RecoverAsync(run.RunId);

        result.Outcome.Should().Be(DurableRunRecoveryOutcome.AutomaticResume);
        result.Run.State.Status.Should().Be(RunStatus.Waiting);
        result.Run.State.WaitReason.Should().Be(RunWaitReason.Retry);
        result.Run.Cursor.NextStepId.Should().Be("read");
        result.Decisions.Should().ContainSingle();
        result.Decisions[0].Action.Should().Be(StepRecoveryAction.RetryAutomatically);
    }

    [Fact]
    public async Task RecoverAsync_WhenFailedAttemptWasPersistedBeforeRunSnapshot_FailsRun()
    {
        var version = CreateVersion(
            new ScenarioStep
            {
                Id = "open",
                Type = StepType.OpenUrl,
                Url = "https://example.com",
            });
        var run = CreateRunningRun(version);
        var started = StepAttempt.Start(
            run.RunId,
            version.MaterializeDefinition().Steps[0],
            1,
            run.UpdatedAt.AddSeconds(1));
        var failed = started.MarkFailed(
            "navigation failed",
            started.StartedAt.AddSeconds(1));
        var runStore = new InMemoryRunStore(run, version);
        var attemptStore = new InMemoryStepAttemptStore(failed);
        var service = CreateService(
            runStore,
            attemptStore,
            run.UpdatedAt.AddMinutes(1));

        var result = await service.RecoverAsync(run.RunId);

        result.Outcome.Should().Be(DurableRunRecoveryOutcome.Failed);
        result.Run.State.Status.Should().Be(RunStatus.Failed);
        result.Run.Cursor.NextStepId.Should().Be("open");
    }

    [Fact]
    public async Task RecoverAsync_WhenFailedAttemptWasPersistedBeforeWait_UsesFailureTimeForRetryDue()
    {
        var version = CreateVersion(
            new ScenarioStep
            {
                Id = "open",
                Type = StepType.OpenUrl,
                Url = "https://example.com",
                RetryCount = 1,
                RetryDelayMs = 30000,
            });
        var run = CreateRunningRun(version);
        var started = StepAttempt.Start(
            run.RunId,
            version.MaterializeDefinition().Steps[0],
            1,
            run.UpdatedAt.AddSeconds(1));
        var failedAt = started.StartedAt.AddSeconds(1);
        var failed = started.MarkFailed(
            "temporary",
            failedAt);
        var runStore = new InMemoryRunStore(run, version);
        var attemptStore = new InMemoryStepAttemptStore(failed);
        var detectedAt = failedAt.AddMinutes(2);
        var service = CreateService(
            runStore,
            attemptStore,
            detectedAt);

        var result = await service.RecoverAsync(run.RunId);

        result.Outcome.Should().Be(DurableRunRecoveryOutcome.AutomaticResume);
        result.Run.State.Status.Should().Be(RunStatus.Waiting);
        result.Run.State.WaitReason.Should().Be(RunWaitReason.Retry);
        result.Run.UpdatedAt.Should().Be(detectedAt);
        result.Run.RetryNotBefore.Should().Be(
            failedAt.AddSeconds(30));
        result.Run.RetryNotBefore.Should().NotBeNull();
        result.Run.RetryNotBefore!.Value.Should().BeBefore(
            result.Run.UpdatedAt);
    }

    [Fact]
    public async Task RecoverAsync_WhenWorkerStoppedBeforeStartingCurrentStep_WaitsForAutomaticResume()
    {
        var version = CreateVersion(
            new ScenarioStep
            {
                Id = "open",
                Type = StepType.OpenUrl,
                Url = "https://example.com",
            });
        var run = CreateRunningRun(version);
        var runStore = new InMemoryRunStore(run, version);
        var attemptStore = new InMemoryStepAttemptStore();
        var service = CreateService(
            runStore,
            attemptStore,
            run.UpdatedAt.AddMinutes(1));

        var result = await service.RecoverAsync(run.RunId);

        result.Outcome.Should().Be(DurableRunRecoveryOutcome.AutomaticResume);
        result.Run.State.Status.Should().Be(RunStatus.Waiting);
        result.Run.State.WaitReason.Should().Be(RunWaitReason.Retry);
        result.Run.Cursor.NextStepId.Should().Be("open");
    }

    [Fact]
    public async Task RecoverAsync_WhenRunIsAlreadyWaiting_DoesNotRewriteIt()
    {
        var version = CreateVersion(
            new ScenarioStep
            {
                Id = "open",
                Type = StepType.OpenUrl,
                Url = "https://example.com",
            });
        var running = CreateRunningRun(version);
        var waiting = AutomationRun.Restore(
            running.RunId,
            version,
            running.State.Wait(RunWaitReason.Event),
            running.Cursor,
            running.Variables,
            running.CreatedAt,
            running.UpdatedAt.AddSeconds(1));
        var runStore = new InMemoryRunStore(waiting, version);
        var attemptStore = new InMemoryStepAttemptStore();
        var service = CreateService(
            runStore,
            attemptStore,
            waiting.UpdatedAt.AddMinutes(1));

        var result = await service.RecoverAsync(waiting.RunId);

        result.Outcome.Should().Be(DurableRunRecoveryOutcome.NoAction);
        result.Run.State.Status.Should().Be(RunStatus.Waiting);
        result.Run.State.WaitReason.Should().Be(RunWaitReason.Event);
        runStore.Saves.Should().BeEmpty();
        attemptStore.MarkUnknownCalls.Should().Be(0);
    }

    [Fact]
    public async Task RecoverAsync_WhenRunDoesNotExist_Throws()
    {
        var runStore = new EmptyRunStore();
        var attemptStore = new InMemoryStepAttemptStore();
        var service = CreateService(
            runStore,
            attemptStore,
            DateTimeOffset.Parse("2026-09-28T08:00:00+02:00"));

        var act = () => service.RecoverAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    private static DurableRunRecoveryService CreateService(
        IRunStore runStore,
        IStepAttemptStore attemptStore,
        DateTimeOffset now) =>
        new(
            runStore,
            attemptStore,
            new FixedTimeProvider(now));

    private static AutomationRun CreateRunningRun(
        ScenarioVersion version)
    {
        var createdAt =
            DateTimeOffset.Parse("2026-09-28T07:00:00+02:00");
        var queued = AutomationRun.Create(
            version,
            createdAt);

        return AutomationRun.RestoreStructured(
            queued.RunId,
            version,
            queued.State.Start(),
            queued.Cursor,
            queued.Variables,
            queued.CreatedAt,
            createdAt.AddSeconds(1));
    }

    private static ScenarioVersion CreateVersion(
        params ScenarioStep[] steps) =>
        ScenarioVersion.Capture(
            Guid.NewGuid(),
            1,
            new ScenarioDefinition
            {
                Name = "Recovery test",
                Steps = [.. steps],
            },
            DateTimeOffset.Parse("2026-09-28T06:00:00+02:00"));

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class InMemoryRunStore : IRunStore
    {
        private StoredAutomationRun _stored;

        public InMemoryRunStore(
            AutomationRun run,
            ScenarioVersion scenarioVersion)
        {
            _stored = new StoredAutomationRun(
                run,
                scenarioVersion);
        }

        public List<AutomationRun> Saves { get; } = [];

        public Task SaveAsync(
            AutomationRun run,
            ScenarioVersion scenarioVersion,
            CancellationToken cancellationToken = default)
        {
            Saves.Add(run);
            _stored = new StoredAutomationRun(
                run,
                scenarioVersion);
            return Task.CompletedTask;
        }

        public Task<StoredAutomationRun?> LoadAsync(
            Guid runId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<StoredAutomationRun?>(
                _stored.Run.RunId == runId
                    ? _stored
                    : null);
    }

    private sealed class EmptyRunStore : IRunStore
    {
        public Task SaveAsync(
            AutomationRun run,
            ScenarioVersion scenarioVersion,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<StoredAutomationRun?> LoadAsync(
            Guid runId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<StoredAutomationRun?>(null);
    }

    private sealed class InMemoryStepAttemptStore : IStepAttemptStore
    {
        public InMemoryStepAttemptStore(
            params StepAttempt[] attempts)
        {
            Attempts = [.. attempts];
        }

        public List<StepAttempt> Attempts { get; }

        public int MarkUnknownCalls { get; private set; }

        public Task SaveStepAttemptAsync(
            StepAttempt attempt,
            CancellationToken cancellationToken = default)
        {
            var index = Attempts.FindIndex(
                candidate => candidate.AttemptId == attempt.AttemptId);

            if (index >= 0)
            {
                Attempts[index] = attempt;
            }
            else
            {
                Attempts.Add(attempt);
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StepAttempt>> LoadStepAttemptsAsync(
            Guid runId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StepAttempt>>(
                Attempts
                    .Where(attempt => attempt.RunId == runId)
                    .ToArray());

        public Task<IReadOnlyList<StepAttempt>> MarkStartedAttemptsUnknownAsync(
            Guid runId,
            DateTimeOffset detectedAt,
            CancellationToken cancellationToken = default)
        {
            MarkUnknownCalls++;
            var changed = new List<StepAttempt>();

            for (var index = 0; index < Attempts.Count; index++)
            {
                var attempt = Attempts[index];

                if (attempt.RunId != runId ||
                    attempt.Status != StepAttemptStatus.Started ||
                    attempt.StartedAt > detectedAt)
                {
                    continue;
                }

                var unknown = attempt.MarkUnknown(detectedAt);
                Attempts[index] = unknown;
                changed.Add(unknown);
            }

            return Task.FromResult<IReadOnlyList<StepAttempt>>(
                changed);
        }
    }
}
