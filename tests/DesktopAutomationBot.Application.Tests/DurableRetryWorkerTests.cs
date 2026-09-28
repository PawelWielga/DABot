using DesktopAutomationBot.Application;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class DurableRetryWorkerTests
{
    [Fact]
    public async Task RunAsync_UsesConfiguredBatchSizeAndStopsWhenCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        var scheduler = new CancellingScheduler(cancellation);
        var worker = new DurableRetryWorker(
            scheduler,
            new BotOptions
            {
                RetryWorker = new RetryWorkerOptions
                {
                    PollIntervalMs = 1,
                    BatchSize = 17,
                },
            });

        await worker.RunAsync(cancellation.Token);

        scheduler.CallCount.Should().Be(1);
        scheduler.BatchSizes.Should().Equal(17);
    }

    [Fact]
    public async Task RunAsync_WhenPollIntervalIsInvalid_ThrowsBeforeRunningScheduler()
    {
        var scheduler = new RecordingScheduler();
        var worker = new DurableRetryWorker(
            scheduler,
            new BotOptions
            {
                RetryWorker = new RetryWorkerOptions
                {
                    PollIntervalMs = 0,
                    BatchSize = 10,
                },
            });

        var act = () => worker.RunAsync();

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*pollIntervalMs*");

        scheduler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_WhenBatchSizeIsInvalid_ThrowsBeforeRunningScheduler()
    {
        var scheduler = new RecordingScheduler();
        var worker = new DurableRetryWorker(
            scheduler,
            new BotOptions
            {
                RetryWorker = new RetryWorkerOptions
                {
                    PollIntervalMs = 1000,
                    BatchSize = 0,
                },
            });

        var act = () => worker.RunAsync();

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*batchSize*");

        scheduler.CallCount.Should().Be(0);
    }

    private sealed class CancellingScheduler : IDurableRetryScheduler
    {
        private readonly CancellationTokenSource _cancellation;

        public CancellingScheduler(CancellationTokenSource cancellation)
        {
            _cancellation = cancellation;
        }

        public int CallCount { get; private set; }

        public List<int> BatchSizes { get; } = [];

        public Task<IReadOnlyList<DurableScenarioExecutionResult>> RunDueRetriesAsync(
            int maxRuns = 100,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            BatchSizes.Add(maxRuns);
            _cancellation.Cancel();

            return Task.FromResult<IReadOnlyList<DurableScenarioExecutionResult>>([]);
        }
    }

    private sealed class RecordingScheduler : IDurableRetryScheduler
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<DurableScenarioExecutionResult>> RunDueRetriesAsync(
            int maxRuns = 100,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyList<DurableScenarioExecutionResult>>([]);
        }
    }
}
