using DesktopAutomationBot.Application;

namespace DesktopAutomationBot.Infrastructure;

public sealed class FileSystemBrowserProfileCatalog(
    BotOptions options) : IBrowserProfileCatalog
{
    public Task<IReadOnlyList<BrowserProfileListItem>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var profilesRoot = Path.GetFullPath(
            options.Storage.BrowserProfilesDirectory);

        if (!Directory.Exists(profilesRoot))
        {
            return Task.FromResult<IReadOnlyList<BrowserProfileListItem>>([]);
        }

        var profiles = Directory
            .EnumerateDirectories(profilesRoot)
            .Select(Path.GetFileName)
            .Where(name =>
                !string.IsNullOrWhiteSpace(name) &&
                !name.StartsWith('.', StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => new BrowserProfileListItem(name!))
            .ToArray();

        return Task.FromResult<IReadOnlyList<BrowserProfileListItem>>(profiles);
    }
}
