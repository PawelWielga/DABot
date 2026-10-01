using DesktopAutomationBot.Application;

namespace DesktopAutomationBot.Web.Demo;

public sealed class DemoStorageRuntimeSettingsService :
    IStorageRuntimeSettingsService
{
    public Task<StorageRuntimeSettings> GetAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            new StorageRuntimeSettings
            {
                ScenariosDirectory = "scenarios",
                ScreenshotsDirectory = "screenshots",
                ArtifactsDirectory = "artifacts",
                BrowserProfilesDirectory = "data/browser-profiles",
                DatabasePath = "data/dabot.db",
            });
    }

    public Task SaveAsync(
        StorageRuntimeSettings settings,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            "Configuration changes are disabled in the static demo.");
}
