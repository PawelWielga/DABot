using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class DurableRunResumeServiceTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(
            Path.GetTempPath(),
            "dabot-resume-tests",
            Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ResumeAsync_FromRetryWait_ReusesRunAndCreatesNextAttemptNumber()
    {
        var version = CreateVersion();
        var running = CreateRunningRun(version);
        var waiting = Restore(
            running,
            version,
            running.State.Wait(RunWaitReason.Retry),
            running.Cursor,
            running.UpdatedAt.AddSeconds(1));
        var step = version.MaterializeDefinition().Steps[0];
        var firstAttempt = StepAttempt.Start(
                waiting.RunId,
                step,
                1,
                waiting.CreatedAt.AddSeconds(2))
            .MarkUnknown(waiting.CreatedAt.AddSeconds(3));

        var runStore = new InMemoryRunStore(waiting, version);
        var attemptStore = new InMemoryStepAttemptStore(firstAttempt);
        var browser = new FakeBrowserAutomation();
        var handler = new FakeStepHandler(StepType.OpenUrl);
        var executor = CreateExecutor(
            [handler],
            browser,
            runStore,
            attemptStore);

        var result = await executor.ResumeAsync(waiting.RunId);

        result.Success.Should().BeTrue();
        result.Run.RunId.Should().Be(waiting.RunId);
        result.Run.ScenarioVersionId.Should().Be(waiting.ScenarioVersionId);
        result.Run.State.Status.Should().Be(RunStatus.Completed);
        result.Run.Cursor.IsCompleted.Should().BeTrue();

        attemptStore.Attempts
            .Where(attempt => attempt.StepId == "open")
            .OrderBy(attempt => attempt.AttemptNumber)
            .Select(attempt => new { attempt.AttemptNumber, attempt.Status })
            .Should()
            .BeEquivalentTo(
                [
                    new
                    {
                        AttemptNumber = 1,
                        Status = StepAttemptStatus.Unknown,
                    },
                    new
                    {
                        AttemptNumber = 2,
                        Status = StepAttemptStatus.Completed,
                    },
                ],
                options => options.WithStrictOrdering());

        handler.ExecutionCount.Should().Be(1);
        browser.OpenCount.Should().Be(1);
        browser.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task ResumeAsync_WhenCursorAlreadyAdvanced_FirstAttemptForNextStepIsOne()
    {
        var version = CreateTwoStepVersion();
        var definition = version.MaterializeDefinition();
        var running = CreateRunningRun(version);

        var firstStarted = StepAttempt.Start(
            running.RunId,
            definition.Steps[0],
            1,
            running.CreatedAt.AddSeconds(2));
        var firstCompleted = firstStarted.MarkCompleted(
            running.CreatedAt.AddSeconds(3));

        var secondCursor = new ExecutionCursor
        {
            NextStepId = "capture",
        };

        var waiting = Restore(
            running,
            version,
            running.State.Wait(RunWaitReason.Retry),
            secondCursor,
            running.UpdatedAt.AddSeconds(1));

        var runStore = new InMemoryRunStore(waiting, version);
        var attemptStore = new InMemoryStepAttemptStore(firstCompleted);
        var executor = CreateExecutor(
            [
                new FakeStepHandler(StepType.OpenUrl),
                new FakeStepHandler(StepType.Screenshot),
            ],
            new FakeBrowserAutomation(),
            runStore,
            attemptStore);

        var result = await executor.ResumeAsync(waiting.RunId);

        result.Run.State.Status.Should().Be(RunStatus.Completed);

        var captureAttempt = attemptStore.Attempts
            .Single(attempt => attempt.StepId == "capture");

        captureAttempt.AttemptNumber.Should().Be(1);
        captureAttempt.Status.Should().Be(StepAttemptStatus.Completed);
    }

    [Fact]
    public async Task ResumeAsync_BeforeRetryNotBefore_RemainsSuspendedWithoutOpeningBrowser()
    {
        var version = CreateVersion(
            new ScenarioStep
            {
                Id = "open",
                Type = StepType.OpenUrl,
                Url = "https://example.com",
                RetryCount = 1,
                RetryDelayMs = 60000,
            });
        var running = CreateRunningRun(version);
        var waitingAt = running.UpdatedAt.AddSeconds(1);
        var retryNotBefore =
            DateTimeOffset.Parse("2026-09-28T09:31:00+02:00");
        var waiting = Restore(
            running,
            version,
            running.State.Wait(RunWaitReason.Retry),
            running.Cursor,
            waitingAt,
            retryNotBefore);
        var firstAttempt = StepAttempt.Start(
                waiting.RunId,
                version.MaterializeDefinition().Steps[0],
                1,
                waiting.CreatedAt.AddSeconds(2))
            .MarkUnknown(waiting.CreatedAt.AddSeconds(3));
        var browser = new FakeBrowserAutomation();
        var handler = new FakeStepHandler(StepType.OpenUrl);
        var executor = CreateExecutor(
            [handler],
            browser,
            new InMemoryRunStore(waiting, version),
            new InMemoryStepAttemptStore(firstAttempt));

        var result = await executor.ResumeAsync(waiting.RunId);

        result.Outcome.Should().Be(DurableExecutionOutcome.Suspended);
        result.Run.State.Status.Should().Be(RunStatus.Waiting);
        result.Run.State.WaitReason.Should().Be(RunWaitReason.Retry);
        result.Run.RetryNotBefore.Should().Be(retryNotBefore);
        handler.ExecutionCount.Should().Be(0);
        browser.OpenCount.Should().Be(0);
    }

    [Fact]
    public async Task ResumeAsync_WhenRetryBudgetIsExhausted_FailsWithoutOpeningBrowser()
    {
        var version = CreateVersion(
            new ScenarioStep
            {
                Id = "open",
                Type = StepType.OpenUrl,
                Url = "https://example.com",
                RetryCount = 0,
            });
        var running = CreateRunningRun(version);
        var waiting = Restore(
            running,
            version,
            running.State.Wait(RunWaitReason.Retry),
            running.Cursor,
            running.UpdatedAt.AddSeconds(1));
        var firstAttempt = StepAttempt.Start(
                waiting.RunId,
                version.MaterializeDefinition().Steps[0],
                1,
                waiting.CreatedAt.AddSeconds(2))
            .MarkUnknown(waiting.CreatedAt.AddSeconds(3));
        var browser = new FakeBrowserAutomation();
        var handler = new FakeStepHandler(StepType.OpenUrl);
        var executor = CreateExecutor(
            [handler],
            browser,
            new InMemoryRunStore(waiting, version),
            new InMemoryStepAttemptStore(firstAttempt));

        var result = await executor.ResumeAsync(waiting.RunId);

        result.Outcome.Should().Be(DurableExecutionOutcome.Failed);
        result.Run.State.Status.Should().Be(RunStatus.Failed);
        result.ErrorMessage.Should().Contain("Retry limit exhausted");
        handler.ExecutionCount.Should().Be(0);
        browser.OpenCount.Should().Be(0);
    }

    [Fact]
    public async Task ResumeAsync_FromHumanWait_IsRejectedWithoutExecuting()
    {
        var version = CreateVersion(
            new ScenarioStep
            {
                Id = "submit",
                Type = StepType.Click,
                Selector = "#submit",
            });
        var running = CreateRunningRun(version);
        var waiting = Restore(
            running,
            version,
            running.State.Wait(RunWaitReason.Human),
            running.Cursor,
            running.UpdatedAt.AddSeconds(1));
        var runStore = new InMemoryRunStore(waiting, version);
        var browser = new FakeBrowserAutomation();
        var handler = new FakeStepHandler(StepType.Click);
        var executor = CreateExecutor(
            [handler],
            browser,
            runStore,
            new InMemoryStepAttemptStore());

        var act = () => executor.ResumeAsync(waiting.RunId);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Waiting / Retry*");

        handler.ExecutionCount.Should().Be(0);
        browser.OpenCount.Should().Be(0);
    }

    [Fact]
    public async Task ResumeAsync_WhenStartedAttemptStillExists_RequiresRecoveryFirst()
    {
        var version = CreateVersion();
        var running = CreateRunningRun(version);
        var waiting = Restore(
            running,
            version,
            running.State.Wait(RunWaitReason.Retry),
            running.Cursor,
            running.UpdatedAt.AddSeconds(1));
        var started = StepAttempt.Start(
            waiting.RunId,
            version.MaterializeDefinition().Steps[0],
            1,
            waiting.CreatedAt.AddSeconds(2));
        var executor = CreateExecutor(
            [new FakeStepHandler(StepType.OpenUrl)],
            new FakeBrowserAutomation(),
            new InMemoryRunStore(waiting, version),
            new InMemoryStepAttemptStore(started));

        var act = () => executor.ResumeAsync(waiting.RunId);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*recovery*");
    }

    [Fact]
    public async Task ResumeAsync_WhenRunDoesNotExist_Throws()
    {
        var executor = CreateExecutor(
            [new FakeStepHandler(StepType.OpenUrl)],
            new FakeBrowserAutomation(),
            new EmptyRunStore(),
            new InMemoryStepAttemptStore());

        var act = () => executor.ResumeAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private DurableScenarioExecutor CreateExecutor(
        IEnumerable<IStepHandler> handlers,
        IBrowserAutomation browser,
        IRunStore runStore,
        IStepAttemptStore attemptStore) =>
        new(
            new BotOptions
            {
                Storage = new StorageOptions
                {
                    ScreenshotsDirectory = Path.Combine(
                        _tempDirectory,
                        "screenshots"),
                },
            },
            new ScenarioValidationService(),
            handlers,
            browser,
            runStore,
            attemptStore,
            new FixedTimeProvider(
                DateTimeOffset.Parse("2026-09-28T09:30:00+02:00")));

    private static AutomationRun CreateRunningRun(
        ScenarioVersion version)
    {
        var createdAt =
            DateTimeOffset.Parse("2026-09-28T08:00:00+02:00");
        var queued = AutomationRun.Create(
            version,
            createdAt);

        return Restore(
            queued,
            version,
            queued.State.Start(),
            queued.Cursor,
            createdAt.AddSeconds(1));
    }

    private static AutomationRun Restore(
        AutomationRun run,
        ScenarioVersion version,
        RunState state,
        ExecutionCursor cursor,
        DateTimeOffset updatedAt,
        DateTimeOffset? retryNotBefore = null) =>
        AutomationRun.Restore(
            run.RunId,
            version,
            state,
            cursor,
            run.Variables,
            run.CreatedAt,
            updatedAt,
            retryNotBefore);

    private static ScenarioVersion CreateVersion(
        ScenarioStep? step = null) =>
        ScenarioVersion.Capture(
            Guid.NewGuid(),
            1,
            new ScenarioDefinition
            {
                Name = "Resume test",
                Steps =
                [
                    step ??
                    new ScenarioStep
                    {
                        Id = "open",
                        Type = StepType.OpenUrl,
                        Url = "https://example.com",
                        RetryCount = 1,
                    },
                ],
            },
            DateTimeOffset.Parse("2026-09-28T07:00:00+02:00"));

    private static ScenarioVersion CreateTwoStepVersion() =>
        ScenarioVersion.Capture(
            Guid.NewGuid(),
            1,
            new ScenarioDefinition
            {
                Name = "Resume two steps",
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
                        Id = "capture",
                        Type = StepType.Screenshot,
                    },
                ],
            },
            DateTimeOffset.Parse("2026-09-28T07:00:00+02:00"));

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
            ScenarioVersion version)
        {
            _stored = new StoredAutomationRun(run, version);
        }

        public Task SaveAsync(
            AutomationRun run,
            ScenarioVersion scenarioVersion,
            CancellationToken cancellationToken = default)
        {
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
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StepAttempt>>([]);
    }

    private sealed class FakeStepHandler : IStepHandler
    {
        public FakeStepHandler(StepType stepType)
        {
            StepType = stepType;
        }

        public StepType StepType { get; }

        public int ExecutionCount { get; private set; }

        public Task<StepExecutionResult> ExecuteAsync(
            ScenarioStep step,
            ScenarioExecutionContext context,
            int index,
            CancellationToken cancellationToken)
        {
            ExecutionCount++;

            return Task.FromResult(
                new StepExecutionResult
                {
                    Index = index,
                    Type = StepType,
                    Success = true,
                });
        }
    }

    private sealed class FakeBrowserAutomation : IBrowserAutomation
    {
        public int OpenCount { get; private set; }

        public int DisposeCount { get; private set; }

        public Task OpenAsync(
            CancellationToken cancellationToken = default)
        {
            OpenCount++;
            return Task.CompletedTask;
        }

        public Task NavigateAsync(
            string url,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ClickAsync(
            string selector,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task FillTextAsync(
            string selector,
            string value,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PasteTextAsync(
            string selector,
            string value,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string> ReadTextAsync(
            string selector,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Empty);

        public Task WaitForSelectorAsync(
            string selector,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task WaitForTextAsync(
            string text,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task WaitForUrlAsync(
            string url,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task WaitForLoadStateAsync(
            string loadState,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string> TakeScreenshotAsync(
            string filePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(filePath);

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
