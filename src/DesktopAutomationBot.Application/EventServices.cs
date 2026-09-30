using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public interface IEventPublisher
{
    Task<EventAcceptanceResult> PublishAsync(
        AutomationEvent automationEvent,
        CancellationToken cancellationToken = default);
}

public sealed class EventPublisher(
    IEventInboxStore eventInboxStore) : IEventPublisher
{
    public Task<EventAcceptanceResult> PublishAsync(
        AutomationEvent automationEvent,
        CancellationToken cancellationToken = default) =>
        eventInboxStore.AcceptAsync(
            automationEvent,
            cancellationToken);
}

public interface IEventResumeWorker
{
    Task<int> RunOnceAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task RunAsync(
        EventWorkerOptions options,
        CancellationToken cancellationToken = default);
}

public sealed class EventResumeWorker(
    IEventInboxStore eventInboxStore,
    IDurableRunControlService runControlService,
    TimeProvider timeProvider) : IEventResumeWorker
{
    public async Task RunAsync(
        EventWorkerOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.PollIntervalMs <= 0)
        {
            throw new InvalidOperationException(
                "Event worker pollIntervalMs must be greater than zero.");
        }

        if (options.BatchSize <= 0)
        {
            throw new InvalidOperationException(
                "Event worker batchSize must be greater than zero.");
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(
                    options.BatchSize,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(options.PollIntervalMs),
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public async Task<int> RunOnceAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var items =
            await eventInboxStore.LoadPendingResumeWorkItemsAsync(
                limit,
                cancellationToken);

        var processed = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await runControlService.ResumeManuallyAsync(
                    item.RunId,
                    cancellationToken);

                await eventInboxStore.MarkResumeWorkItemCompletedAsync(
                    item.WorkItemId,
                    timeProvider.GetUtcNow(),
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                await eventInboxStore.MarkResumeWorkItemFailedAsync(
                    item.WorkItemId,
                    exception.Message,
                    timeProvider.GetUtcNow(),
                    CancellationToken.None);
            }

            processed++;
        }

        return processed;
    }
}
