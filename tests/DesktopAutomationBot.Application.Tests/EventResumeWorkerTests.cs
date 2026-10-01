using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class EventResumeWorkerTests
{
    [Fact]
    public async Task RunOnceAsync_WhenResumeSucceeds_CompletesWorkItem()
    {
        var runId = Guid.NewGuid();
        var workItem = CreateWorkItem(runId);
        var inbox = new RecordingInboxStore(workItem);
        var control = new RecordingRunControlService();
        var worker = new EventResumeWorker(
            inbox,
            control,
            new FixedTimeProvider(
                DateTimeOffset.Parse("2026-09-30T10:00:00+02:00")));

        var processed = await worker.RunOnceAsync();

        processed.Should().Be(1);
        control.ResumedRunIds.Should().Equal(runId);
        inbox.CompletedWorkItems.Should().Equal(workItem.WorkItemId);
        inbox.RetryWorkItems.Should().BeEmpty();
        inbox.DeadLetterWorkItems.Should().BeEmpty();
    }

    [Fact]
    public async Task RunOnceAsync_WhenResumeFails_SchedulesRetryWithExponentialBackoff()
    {
        var workItem = CreateWorkItem(Guid.NewGuid());
        var inbox = new RecordingInboxStore(workItem);
        var control = new RecordingRunControlService
        {
            ResumeException = new InvalidOperationException("resume failed"),
        };
        var now = DateTimeOffset.Parse("2026-09-30T10:05:00+02:00");
        var worker = new EventResumeWorker(
            inbox,
            control,
            new FixedTimeProvider(now),
            new BotOptions
            {
                EventWorker = new EventWorkerOptions
                {
                    MaxAttempts = 5,
                    BaseRetryDelayMs = 1000,
                    MaxRetryDelayMs = 60_000,
                },
            });

        var processed = await worker.RunOnceAsync();

        processed.Should().Be(1);
        inbox.CompletedWorkItems.Should().BeEmpty();
        inbox.RetryWorkItems.Should().ContainSingle();
        inbox.RetryWorkItems[0].WorkItemId.Should().Be(workItem.WorkItemId);
        inbox.RetryWorkItems[0].ErrorMessage.Should().Be("resume failed");
        inbox.RetryWorkItems[0].NextAttemptAt.Should().Be(now.AddSeconds(1));
        inbox.DeadLetterWorkItems.Should().BeEmpty();
    }



    [Fact]
    public async Task RunOnceAsync_WhenMaxAttemptsReached_DeadLettersWorkItem()
    {
        var workItem = CreateWorkItem(Guid.NewGuid()) with
        {
            AttemptCount = 2,
        };
        var inbox = new RecordingInboxStore(workItem);
        var control = new RecordingRunControlService
        {
            ResumeException = new InvalidOperationException("still failing"),
        };
        var now = DateTimeOffset.Parse("2026-09-30T10:10:00+02:00");
        var worker = new EventResumeWorker(
            inbox,
            control,
            new FixedTimeProvider(now),
            new BotOptions
            {
                EventWorker = new EventWorkerOptions
                {
                    MaxAttempts = 3,
                    BaseRetryDelayMs = 1000,
                    MaxRetryDelayMs = 60_000,
                },
            });

        var processed = await worker.RunOnceAsync();

        processed.Should().Be(1);
        inbox.RetryWorkItems.Should().BeEmpty();
        inbox.DeadLetterWorkItems.Should().ContainSingle();
        inbox.DeadLetterWorkItems[0].WorkItemId.Should().Be(workItem.WorkItemId);
        inbox.DeadLetterWorkItems[0].ErrorMessage.Should().Be("still failing");
        inbox.DeadLetterWorkItems[0].FinishedAt.Should().Be(now);
    }

    [Theory]
    [InlineData(1, 1000)]
    [InlineData(2, 2000)]
    [InlineData(3, 4000)]
    [InlineData(10, 5000)]
    public void CalculateRetryDelay_AppliesExponentialBackoffAndCap(
        int failedAttempt,
        int expectedDelayMs)
    {
        var delay = EventResumeWorker.CalculateRetryDelay(
            failedAttempt,
            new EventWorkerOptions
            {
                MaxAttempts = 20,
                BaseRetryDelayMs = 1000,
                MaxRetryDelayMs = 5000,
            });

        delay.Should().Be(TimeSpan.FromMilliseconds(expectedDelayMs));
    }

    [Theory]
    [InlineData(0, 100, "pollIntervalMs")]
    [InlineData(1000, 0, "batchSize")]
    public async Task RunAsync_WhenOptionsAreInvalid_Throws(
        int pollIntervalMs,
        int batchSize,
        string expectedSetting)
    {
        var worker = new EventResumeWorker(
            new RecordingInboxStore(),
            new RecordingRunControlService(),
            new FixedTimeProvider(DateTimeOffset.UtcNow));

        var action = () => worker.RunAsync(
            new EventWorkerOptions
            {
                PollIntervalMs = pollIntervalMs,
                BatchSize = batchSize,
            });

        await action.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{expectedSetting}*");
    }

    private static ResumeWorkItem CreateWorkItem(Guid runId) =>
        new()
        {
            WorkItemId = Guid.NewGuid(),
            RunId = runId,
            EventId = Guid.NewGuid(),
            Status = ResumeWorkItemStatus.Pending,
            CreatedAt = DateTimeOffset.Parse(
                "2026-09-30T09:55:00+02:00"),
        };

    private sealed class FixedTimeProvider(
        DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingInboxStore(
        params ResumeWorkItem[] workItems) : IEventInboxStore
    {
        public List<Guid> CompletedWorkItems { get; } = [];

        public List<(Guid WorkItemId, string ErrorMessage, DateTimeOffset NextAttemptAt)> RetryWorkItems { get; } = [];

        public List<(Guid WorkItemId, string ErrorMessage, DateTimeOffset FinishedAt)> DeadLetterWorkItems { get; } = [];

        public Task ArmEventWaitAsync(
            AutomationRun run,
            ScenarioVersion scenarioVersion,
            EventWaitRegistration wait,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<EventAcceptanceResult> AcceptAsync(
            AutomationEvent automationEvent,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ResumeWorkItem>> LoadPendingResumeWorkItemsAsync(
            DateTimeOffset dueAt,
            int limit = 100,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ResumeWorkItem>>(
                workItems
                    .Where(item =>
                        item.NextAttemptAt is null ||
                        item.NextAttemptAt <= dueAt)
                    .Take(limit)
                    .ToArray());

        public Task<IReadOnlyList<ResumeWorkItem>> LoadDeadLetterResumeWorkItemsAsync(
            int limit = 100,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ResumeWorkItem>>([]);

        public Task MarkResumeWorkItemCompletedAsync(
            Guid workItemId,
            DateTimeOffset finishedAt,
            CancellationToken cancellationToken = default)
        {
            CompletedWorkItems.Add(workItemId);
            return Task.CompletedTask;
        }

        public Task ScheduleResumeWorkItemRetryAsync(
            Guid workItemId,
            string errorMessage,
            DateTimeOffset nextAttemptAt,
            CancellationToken cancellationToken = default)
        {
            RetryWorkItems.Add(
                (workItemId, errorMessage, nextAttemptAt));
            return Task.CompletedTask;
        }

        public Task DeadLetterResumeWorkItemAsync(
            Guid workItemId,
            string errorMessage,
            DateTimeOffset finishedAt,
            CancellationToken cancellationToken = default)
        {
            DeadLetterWorkItems.Add(
                (workItemId, errorMessage, finishedAt));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingRunControlService : IDurableRunControlService
    {
        public List<Guid> ResumedRunIds { get; } = [];

        public Exception? ResumeException { get; init; }

        public Task<DurableScenarioExecutionResult> CloneAsync(
            Guid runId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DurableScenarioExecutionResult> ResumeManuallyAsync(
            Guid runId,
            CancellationToken cancellationToken = default)
        {
            ResumedRunIds.Add(runId);

            if (ResumeException is not null)
            {
                throw ResumeException;
            }

            return Task.FromResult(
                new DurableScenarioExecutionResult
                {
                    Run = null!,
                    Outcome = DurableExecutionOutcome.Completed,
                });
        }

        public Task<DurableScenarioExecutionResult> RetryNowAsync(
            Guid runId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AutomationRun> CancelAsync(
            Guid runId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
