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
        inbox.FailedWorkItems.Should().BeEmpty();
    }

    [Fact]
    public async Task RunOnceAsync_WhenResumeFails_MarksWorkItemFailed()
    {
        var workItem = CreateWorkItem(Guid.NewGuid());
        var inbox = new RecordingInboxStore(workItem);
        var control = new RecordingRunControlService
        {
            ResumeException = new InvalidOperationException("resume failed"),
        };
        var worker = new EventResumeWorker(
            inbox,
            control,
            new FixedTimeProvider(
                DateTimeOffset.Parse("2026-09-30T10:05:00+02:00")));

        var processed = await worker.RunOnceAsync();

        processed.Should().Be(1);
        inbox.CompletedWorkItems.Should().BeEmpty();
        inbox.FailedWorkItems.Should().ContainSingle();
        inbox.FailedWorkItems[0].WorkItemId.Should().Be(workItem.WorkItemId);
        inbox.FailedWorkItems[0].ErrorMessage.Should().Be("resume failed");
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

        public List<(Guid WorkItemId, string ErrorMessage)> FailedWorkItems { get; } = [];

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
            int limit = 100,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ResumeWorkItem>>(
                workItems.Take(limit).ToArray());

        public Task MarkResumeWorkItemCompletedAsync(
            Guid workItemId,
            DateTimeOffset finishedAt,
            CancellationToken cancellationToken = default)
        {
            CompletedWorkItems.Add(workItemId);
            return Task.CompletedTask;
        }

        public Task MarkResumeWorkItemFailedAsync(
            Guid workItemId,
            string errorMessage,
            DateTimeOffset finishedAt,
            CancellationToken cancellationToken = default)
        {
            FailedWorkItems.Add((workItemId, errorMessage));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingRunControlService : IDurableRunControlService
    {
        public List<Guid> ResumedRunIds { get; } = [];

        public Exception? ResumeException { get; init; }

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

        public Task<AutomationRun> CancelAsync(
            Guid runId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
