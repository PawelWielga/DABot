using DesktopAutomationBot.Application;

namespace DesktopAutomationBot.Web.Demo;

public sealed class DemoBrowserProfileCatalog : IBrowserProfileCatalog
{
    private static readonly IReadOnlyList<BrowserProfileListItem> Profiles =
    [
        new("backoffice"),
        new("shop-account"),
        new("testing"),
    ];

    public Task<IReadOnlyList<BrowserProfileListItem>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Profiles);
    }
}
