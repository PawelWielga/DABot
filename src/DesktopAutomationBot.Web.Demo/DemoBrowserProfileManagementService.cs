using DesktopAutomationBot.Application;

namespace DesktopAutomationBot.Web.Demo;

public sealed class DemoBrowserProfileManagementService : IBrowserProfileManagementService
{
    public Task CreateAsync(
        string profileName,
        CancellationToken cancellationToken = default) =>
        Unavailable(cancellationToken);

    public Task RenameAsync(
        string profileName,
        string newProfileName,
        CancellationToken cancellationToken = default) =>
        Unavailable(cancellationToken);

    public Task DeleteAsync(
        string profileName,
        CancellationToken cancellationToken = default) =>
        Unavailable(cancellationToken);

    public Task ClearAsync(
        string profileName,
        CancellationToken cancellationToken = default) =>
        Unavailable(cancellationToken);

    private static Task Unavailable(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromException(
            new NotSupportedException(
                "Browser profile mutations are not available in the static demo."));
    }
}
