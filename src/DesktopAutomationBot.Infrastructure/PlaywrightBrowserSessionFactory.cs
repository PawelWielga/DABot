using DesktopAutomationBot.Application;

namespace DesktopAutomationBot.Infrastructure;

public sealed class PlaywrightBrowserSessionFactory(
    BotOptions options) : IBrowserSessionFactory
{
    public ValueTask<IBrowserSession> CreateAsync(
        CancellationToken cancellationToken = default) =>
        CreateAsync(
            new BrowserSessionRequest(),
            cancellationToken);

    public ValueTask<IBrowserSession> CreateAsync(
        BrowserSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!request.IsPersistent)
        {
            return ValueTask.FromResult<IBrowserSession>(
                new PlaywrightBrowserAutomation(
                options,
                request.Headless));
        }

        var profileName = request.ProfileName!;
        BrowserProfileNameRules.Validate(profileName);

        var profilesRoot = Path.GetFullPath(
            options.Storage.BrowserProfilesDirectory);
        var profileDirectory = Path.Combine(
            profilesRoot,
            profileName);
        var locksDirectory = Path.Combine(
            profilesRoot,
            ".locks");

        Directory.CreateDirectory(locksDirectory);

        var lease = BrowserProfileLease.Acquire(
            Path.Combine(
                locksDirectory,
                $"{profileName}.lock"));

        try
        {
            Directory.CreateDirectory(profileDirectory);
            BrowserProfileDirectoryGuard.EnsureNotReparsePoint(
                profileDirectory,
                profileName);

            return ValueTask.FromResult<IBrowserSession>(
                new PlaywrightBrowserAutomation(
                    options,
                    profileDirectory,
                    lease,
                    request.Headless));
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }
}
