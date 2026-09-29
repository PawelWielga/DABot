using DesktopAutomationBot.Application;

namespace DesktopAutomationBot.Infrastructure;

public sealed class PlaywrightBrowserSessionFactory(
    BotOptions options) : IBrowserSessionFactory
{
    public ValueTask<IBrowserSession> CreateAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult<IBrowserSession>(
            new PlaywrightBrowserAutomation(options));
    }
}
