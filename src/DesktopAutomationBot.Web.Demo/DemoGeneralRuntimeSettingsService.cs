using DesktopAutomationBot.Application;

namespace DesktopAutomationBot.Web.Demo;

public sealed class DemoGeneralRuntimeSettingsService :
    IGeneralRuntimeSettingsService
{
    public Task<GeneralRuntimeSettings> GetAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            new GeneralRuntimeSettings
            {
                ScenarioPath = "scenarios/sample-open-url.json",
                BrowserHeadless = true,
                BrowserSlowMoMs = 0,
                BrowserTimeoutMs = 30_000,
                BrowserViewportWidth = 1280,
                BrowserViewportHeight = 720,
                RetryWorkerPollIntervalMs = 1000,
                RetryWorkerBatchSize = 100,
                EventWorkerPollIntervalMs = 1000,
                EventWorkerBatchSize = 100,
                EventWorkerMaxAttempts = 5,
                EventWorkerBaseRetryDelayMs = 1000,
                EventWorkerMaxRetryDelayMs = 60_000,
                ObserverWorkerPollIntervalMs = 1000,
                ObserverWorkerBatchSize = 50,
                ObserverWorkerMaxErrorBackoffMs = 60_000,
                InteractiveBrowserMaxDurationSeconds = 1800,
            });
    }

    public Task SaveAsync(
        GeneralRuntimeSettings settings,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            "Configuration changes are disabled in the static demo.");
}
