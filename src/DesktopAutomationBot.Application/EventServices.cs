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
}

public sealed class EventResumeWorker(
    IEventInboxStore eventInboxStore,
    IDurableRunControlService runControlService,
    TimeProvider timeProvider) : IEventResumeWorker
{
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
