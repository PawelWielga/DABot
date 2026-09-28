using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class DurableRetrySchedulerTests
{
    [Fact]
    public async Task RunDueRetriesAsync_QueriesCurrentTimeAndResumesRunsInStoreOrder()
    {
        var now = DateTimeOffset.Parse("2026-09-28T11:45:00+02:00");
        var firstRunId = Guid.NewGuid();
        var secondRunId = Guid.NewGuid();
        var store = new FakeRetryRunStore(firstRunId, secondRunId);
        var resumeService = new FakeResumeService(now);
        var scheduler = new DurableRetryScheduler(
            store,
            resumeService,
            new FixedTimeProvider(now));

        var results = await scheduler.RunDueRetriesAsync(maxRuns: 25);

        store.DueAt.Should().Be(now);
        store.Limit.Should().Be(25);
        resumeService.ResumedRunIds.Should().Equal(firstRunId, secondRunId);
        results.Select(result => result.Run.RunId)
            .Should().Equal(firstRunId, secondRunId);
    }

    [Fact]
    public async Task RunDueRetriesAsync_WhenShutdownIsRequestedDuringResume_CompletesCurrentRunAndStopsBeforeNext()
    {
        var now = DateTimeOffset.Parse("2026-09-28T11:50:00+02:00");
        var firstRunId = Guid.NewGuid();
        var secondRunId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var resumeService = new CancellingResumeService(
            now,
            cancellation);
        var scheduler = new DurableRetryScheduler(
            new FakeRetryRunStore(firstRunId, secondRunId),
            resumeService,
            new FixedTimeProvider(now));

        var results = await scheduler.RunDueRetriesAsync(
            maxRuns: 25,
            cancellation.Token);

        resumeService.ResumedRunIds.Should().Equal(firstRunId);
        resumeService.ReceivedCancellationTokens
            .Should().OnlyContain(token => !token.CanBeCanceled);
        results.Select(result => result.Run.RunId)
            .Should().Equal(firstRunId);
    }

    [Fact]
    public async Task RunDueRetriesAsync_WhenMaxRunsIsNotPositive_ThrowsBeforeQueryingStore()
    {
        var store = new FakeRetryRunStore(Guid.NewGuid());
        var scheduler = new DurableRetryScheduler(
            store,
            new FakeResumeService(DateTimeOffset.UtcNow),
            new FixedTimeProvider(DateTimeOffset.UtcNow));

        var act = () => scheduler.RunDueRetriesAsync(maxRuns: 0);

        await act.Should()
            .ThrowAsync<ArgumentOutOfRangeException>();

        store.QueryCount.Should().Be(0);
    }

    private sealed class FakeRetryRunStore : IRetryRunStore
    {
        private readonly IReadOnlyList<Guid> _runIds;

        public FakeRetryRunStore(params Guid[] runIds)
        {
            _runIds = runIds;
        }

        public DateTimeOffset? DueAt { get; private set; }

        public int? Limit { get; private set; }

        public int QueryCount { get; private set; }

        public Task<IReadOnlyList<Guid>> LoadDueRetryRunIdsAsync(
            DateTimeOffset dueAt,
            int limit = 100,
            CancellationToken cancellationToken = default)
        {
            QueryCount++;
            DueAt = dueAt;
            Limit = limit;
            return Task.FromResult(_runIds);
        }
    }

    private sealed class FakeResumeService : IDurableRunResumeService
    {
        private readonly DateTimeOffset _now;

        public FakeResumeService(DateTimeOffset now)
        {
            _now = now;
        }

        public List<Guid> ResumedRunIds { get; } = [];

        public Task<DurableScenarioExecutionResult> ResumeAsync(
            Guid runId,
            CancellationToken cancellationToken = default)
        {
            ResumedRunIds.Add(runId);

            var version = ScenarioVersion.Capture(
                Guid.NewGuid(),
                1,
                new ScenarioDefinition
                {
                    Name = "Retry scheduler test",
                    Steps =
                    [
                        new ScenarioStep
                        {
                            Id = "capture",
                            Type = StepType.Screenshot,
                        },
                    ],
                },
                _now);

            var run = AutomationRun.Create(
                version,
                _now,
                runId);

            return Task.FromResult(
                new DurableScenarioExecutionResult
                {
                    Run = run,
                    Outcome = DurableExecutionOutcome.Suspended,
                });
        }
    }

    private sealed class CancellingResumeService : IDurableRunResumeService
    {
        private readonly DateTimeOffset _now;
        private readonly CancellationTokenSource _cancellation;

        public CancellingResumeService(
            DateTimeOffset now,
            CancellationTokenSource cancellation)
        {
            _now = now;
            _cancellation = cancellation;
        }

        public List<Guid> ResumedRunIds { get; } = [];

        public List<CancellationToken> ReceivedCancellationTokens { get; } = [];

        public Task<DurableScenarioExecutionResult> ResumeAsync(
            Guid runId,
            CancellationToken cancellationToken = default)
        {
            ResumedRunIds.Add(runId);
            ReceivedCancellationTokens.Add(cancellationToken);
            _cancellation.Cancel();

            var version = ScenarioVersion.Capture(
                Guid.NewGuid(),
                1,
                new ScenarioDefinition
                {
                    Name = "Graceful shutdown test",
                    Steps =
                    [
                        new ScenarioStep
                        {
                            Id = "capture",
                            Type = StepType.Screenshot,
                        },
                    ],
                },
                _now);

            return Task.FromResult(
                new DurableScenarioExecutionResult
                {
                    Run = AutomationRun.Create(
                        version,
                        _now,
                        runId),
                    Outcome = DurableExecutionOutcome.Completed,
                });
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
