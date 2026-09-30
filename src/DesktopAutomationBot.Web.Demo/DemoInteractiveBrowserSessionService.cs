using DesktopAutomationBot.Application;

namespace DesktopAutomationBot.Web.Demo;

public sealed class DemoInteractiveBrowserSessionService :
    IInteractiveBrowserSessionService
{
    public Task<IReadOnlyList<InteractiveBrowserSessionInfo>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<InteractiveBrowserSessionInfo>>([]);
    }

    public Task<InteractiveBrowserSessionInfo> StartAsync(
        string profileName,
        string? url = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromException<InteractiveBrowserSessionInfo>(
            new NotSupportedException(
                "Interactive browser sessions are not available in the static demo."));
    }

    public Task<bool> StopAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }
}
