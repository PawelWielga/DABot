using DesktopAutomationBot.Application;

namespace DesktopAutomationBot.Web.Demo;

public sealed class DemoAdministrativeEventService :
    IAdministrativeEventService
{
    private const string Message =
        "Event publishing is disabled in the static GitHub Pages demo.";

    public Task<AdministrativeEventPublishResult> PublishAsync(
        AdministrativeEventPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            new AdministrativeEventPublishResult(
                Success: false,
                Errors: [Message]));
    }
}
