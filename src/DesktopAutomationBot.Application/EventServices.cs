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

public sealed class EventResumeWorker : IEventResumeWorker
{
    private readonly IEventInboxStore _eventInboxStore;
    private readonly IDurableRunControlService _runControlService;
    private readonly TimeProvider _timeProvider;
    private readonly EventWorkerOptions _defaultOptions;

    public EventResumeWorker(
        IEventInboxStore eventInboxStore,
        IDurableRunControlService runControlService,
        TimeProvider timeProvider,
        BotOptions? options = null)
    {
        _eventInboxStore = eventInboxStore;
        _runControlService = runControlService;
        _timeProvider = timeProvider;
        _defaultOptions = options?.EventWorker ?? new EventWorkerOptions();
    }

    public async Task RunAsync(
        EventWorkerOptions options,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(
                    options.BatchSize,
                    options,
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

    public Task<int> RunOnceAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions(_defaultOptions);
        return RunOnceAsync(
            limit,
            _defaultOptions,
            cancellationToken);
    }

    private async Task<int> RunOnceAsync(
        int limit,
        EventWorkerOptions options,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var items =
            await _eventInboxStore.LoadPendingResumeWorkItemsAsync(
                now,
                limit,
                cancellationToken);

        var processed = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await _runControlService.ResumeManuallyAsync(
                    item.RunId,
                    cancellationToken);

                await _eventInboxStore.MarkResumeWorkItemCompletedAsync(
                    item.WorkItemId,
                    _timeProvider.GetUtcNow(),
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var failedAttempt = item.AttemptCount + 1;
                var failedAt = _timeProvider.GetUtcNow();

                if (failedAttempt >= options.MaxAttempts)
                {
                    await _eventInboxStore.DeadLetterResumeWorkItemAsync(
                        item.WorkItemId,
                        exception.Message,
                        failedAt,
                        CancellationToken.None);
                }
                else
                {
                    var delay = CalculateRetryDelay(
                        failedAttempt,
                        options);

                    await _eventInboxStore.ScheduleResumeWorkItemRetryAsync(
                        item.WorkItemId,
                        exception.Message,
                        failedAt.Add(delay),
                        CancellationToken.None);
                }
            }

            processed++;
        }

        return processed;
    }

    internal static TimeSpan CalculateRetryDelay(
        int failedAttempt,
        EventWorkerOptions options)
    {
        if (failedAttempt <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failedAttempt));
        }

        ValidateOptions(options);

        if (options.BaseRetryDelayMs == 0)
        {
            return TimeSpan.Zero;
        }

        var exponent = Math.Min(
            failedAttempt - 1,
            30);
        var multiplier = 1L << exponent;
        var delayMs = Math.Min(
            (long)options.MaxRetryDelayMs,
            (long)options.BaseRetryDelayMs * multiplier);

        return TimeSpan.FromMilliseconds(delayMs);
    }

    private static void ValidateOptions(
        EventWorkerOptions options)
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

        if (options.MaxAttempts <= 0)
        {
            throw new InvalidOperationException(
                "Event worker maxAttempts must be greater than zero.");
        }

        if (options.BaseRetryDelayMs < 0)
        {
            throw new InvalidOperationException(
                "Event worker baseRetryDelayMs must not be negative.");
        }

        if (options.MaxRetryDelayMs < options.BaseRetryDelayMs)
        {
            throw new InvalidOperationException(
                "Event worker maxRetryDelayMs must be greater than or equal to baseRetryDelayMs.");
        }
    }
}
