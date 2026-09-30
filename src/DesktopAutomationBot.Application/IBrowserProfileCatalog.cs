namespace DesktopAutomationBot.Application;

public sealed record BrowserProfileListItem(
    string Name);

public interface IBrowserProfileCatalog
{
    Task<IReadOnlyList<BrowserProfileListItem>> ListAsync(
        CancellationToken cancellationToken = default);
}
