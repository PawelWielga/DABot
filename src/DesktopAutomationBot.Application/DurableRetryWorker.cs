namespace DesktopAutomationBot.Application;

public sealed class DurableRetryWorker : IDurableRetryWorker
{
    private readonly IDurableRetryScheduler _scheduler;
    private readonly RetryWorkerOptions _options;

    public DurableRetryWorker(
        IDurableRetryScheduler scheduler,
        BotOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _scheduler = scheduler;
        _options = options.RetryWorker;
    }

    public async Task RunAsync(
        CancellationToken cancellationToken = default)
    {
        ValidateOptions();

        while (!cancellationToken.IsCancellationRequested)
        {
            await _scheduler.RunDueRetriesAsync(
                _options.BatchSize,
                cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(_options.PollIntervalMs),
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private void ValidateOptions()
    {
        if (_options.PollIntervalMs <= 0)
        {
            throw new InvalidOperationException(
                "Retry worker pollIntervalMs must be greater than zero.");
        }

        if (_options.BatchSize <= 0)
        {
            throw new InvalidOperationException(
                "Retry worker batchSize must be greater than zero.");
        }
    }
}
