namespace DesktopAutomationBot.Application;

public interface IGeneralRuntimeSettingsService
{
    Task<GeneralRuntimeSettings> GetAsync(
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        GeneralRuntimeSettings settings,
        CancellationToken cancellationToken = default);
}

public sealed class GeneralRuntimeSettings
{
    public string? ScenarioPath { get; set; }

    public bool BrowserHeadless { get; set; } = true;

    public int BrowserSlowMoMs { get; set; }

    public int BrowserTimeoutMs { get; set; } = 30_000;

    public int BrowserViewportWidth { get; set; } = 1280;

    public int BrowserViewportHeight { get; set; } = 720;

    public int RetryWorkerPollIntervalMs { get; set; } = 1000;

    public int RetryWorkerBatchSize { get; set; } = 100;

    public int EventWorkerPollIntervalMs { get; set; } = 1000;

    public int EventWorkerBatchSize { get; set; } = 100;

    public int EventWorkerMaxAttempts { get; set; } = 5;

    public int EventWorkerBaseRetryDelayMs { get; set; } = 1000;

    public int EventWorkerMaxRetryDelayMs { get; set; } = 60_000;

    public int ObserverWorkerPollIntervalMs { get; set; } = 1000;

    public int ObserverWorkerBatchSize { get; set; } = 50;

    public int ObserverWorkerMaxErrorBackoffMs { get; set; } = 60_000;

    public int InteractiveBrowserMaxDurationSeconds { get; set; } = 1800;

    public static GeneralRuntimeSettings FromOptions(BotOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new GeneralRuntimeSettings
        {
            ScenarioPath = options.ScenarioPath,
            BrowserHeadless = options.Browser.Headless,
            BrowserSlowMoMs = options.Browser.SlowMoMs,
            BrowserTimeoutMs = options.Browser.TimeoutMs,
            BrowserViewportWidth = options.Browser.ViewportWidth,
            BrowserViewportHeight = options.Browser.ViewportHeight,
            RetryWorkerPollIntervalMs = options.RetryWorker.PollIntervalMs,
            RetryWorkerBatchSize = options.RetryWorker.BatchSize,
            EventWorkerPollIntervalMs = options.EventWorker.PollIntervalMs,
            EventWorkerBatchSize = options.EventWorker.BatchSize,
            EventWorkerMaxAttempts = options.EventWorker.MaxAttempts,
            EventWorkerBaseRetryDelayMs = options.EventWorker.BaseRetryDelayMs,
            EventWorkerMaxRetryDelayMs = options.EventWorker.MaxRetryDelayMs,
            ObserverWorkerPollIntervalMs = options.ObserverWorker.PollIntervalMs,
            ObserverWorkerBatchSize = options.ObserverWorker.BatchSize,
            ObserverWorkerMaxErrorBackoffMs = options.ObserverWorker.MaxErrorBackoffMs,
            InteractiveBrowserMaxDurationSeconds = options.InteractiveBrowser.MaxDurationSeconds,
        };
    }
}

public static class GeneralRuntimeSettingsValidator
{
    public static IReadOnlyList<string> Validate(
        GeneralRuntimeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var errors = new List<string>();

        if (settings.BrowserSlowMoMs < 0)
        {
            errors.Add("Browser slow motion must be zero or greater.");
        }

        if (settings.BrowserTimeoutMs < 0)
        {
            errors.Add("Browser timeout must be zero or greater.");
        }

        if (settings.BrowserViewportWidth < 1 ||
            settings.BrowserViewportHeight < 1)
        {
            errors.Add("Browser viewport width and height must be at least 1.");
        }

        ValidatePositive(
            settings.RetryWorkerPollIntervalMs,
            "Retry worker poll interval",
            errors);
        ValidatePositive(
            settings.RetryWorkerBatchSize,
            "Retry worker batch size",
            errors);

        ValidatePositive(
            settings.EventWorkerPollIntervalMs,
            "Event worker poll interval",
            errors);
        ValidatePositive(
            settings.EventWorkerBatchSize,
            "Event worker batch size",
            errors);
        ValidatePositive(
            settings.EventWorkerMaxAttempts,
            "Event worker maximum attempts",
            errors);

        if (settings.EventWorkerBaseRetryDelayMs < 0)
        {
            errors.Add("Event worker base retry delay must be zero or greater.");
        }

        if (settings.EventWorkerMaxRetryDelayMs < 0)
        {
            errors.Add("Event worker maximum retry delay must be zero or greater.");
        }

        if (settings.EventWorkerMaxRetryDelayMs <
            settings.EventWorkerBaseRetryDelayMs)
        {
            errors.Add(
                "Event worker maximum retry delay must be greater than or equal to the base retry delay.");
        }

        ValidatePositive(
            settings.ObserverWorkerPollIntervalMs,
            "Observer worker poll interval",
            errors);
        ValidatePositive(
            settings.ObserverWorkerBatchSize,
            "Observer worker batch size",
            errors);
        ValidatePositive(
            settings.ObserverWorkerMaxErrorBackoffMs,
            "Observer worker maximum error backoff",
            errors);
        ValidatePositive(
            settings.InteractiveBrowserMaxDurationSeconds,
            "Interactive browser maximum duration",
            errors);

        return errors;
    }

    public static void ValidateOrThrow(
        GeneralRuntimeSettings settings)
    {
        var errors = Validate(settings);

        if (errors.Count > 0)
        {
            throw new ArgumentException(
                string.Join(Environment.NewLine, errors),
                nameof(settings));
        }
    }

    private static void ValidatePositive(
        int value,
        string name,
        ICollection<string> errors)
    {
        if (value < 1)
        {
            errors.Add($"{name} must be at least 1.");
        }
    }
}
