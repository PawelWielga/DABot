using DesktopAutomationBot.Application;

namespace DesktopAutomationBot.Web.Demo;

public sealed class DemoBrowserProfileService : IBrowserProfileService
{
    public ValueTask<IInteractiveBrowserSession> OpenInteractiveAsync(
        string profileName,
        string? url = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            "Interactive browser sessions are not available in the static demo.");

    public Task<BrowserProfileTestResult> TestAsync(
        string profileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            new BrowserProfileTestResult
            {
                ProfileName = profileName,
                Success = false,
                ErrorMessage = "Runtime profile tests are not available in the static demo.",
            });
    }
}
