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
        ValidateProfileName(profileName);

        var profilesRoot = Path.GetFullPath(
            options.Storage.BrowserProfilesDirectory);
        var profileDirectory = Path.Combine(
            profilesRoot,
            profileName);
        var locksDirectory = Path.Combine(
            profilesRoot,
            ".locks");

        Directory.CreateDirectory(profileDirectory);
        Directory.CreateDirectory(locksDirectory);

        var lease = BrowserProfileLease.Acquire(
            Path.Combine(
                locksDirectory,
                $"{profileName}.lock"));

        return ValueTask.FromResult<IBrowserSession>(
            new PlaywrightBrowserAutomation(
                options,
                profileDirectory,
                lease,
                request.Headless));
    }

    private static void ValidateProfileName(string profileName)
    {
        if (profileName.Length is < 1 or > 64 ||
            !char.IsLetterOrDigit(profileName[0]) ||
            profileName.Any(character =>
                !char.IsLetterOrDigit(character) &&
                character is not '.' and not '_' and not '-'))
        {
            throw new ArgumentException(
                "Browser profile names must start with an alphanumeric character and contain only alphanumeric characters, '.', '_' or '-' (maximum 64 characters).",
                nameof(profileName));
        }
    }

    private sealed class BrowserProfileLease : IAsyncDisposable
    {
        private readonly FileStream _stream;

        private BrowserProfileLease(FileStream stream)
        {
            _stream = stream;
        }

        public static BrowserProfileLease Acquire(string lockFilePath)
        {
            try
            {
                return new BrowserProfileLease(
                    new FileStream(
                        lockFilePath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None));
            }
            catch (IOException exception)
            {
                throw new InvalidOperationException(
                    $"Browser profile '{Path.GetFileNameWithoutExtension(lockFilePath)}' is already in use.",
                    exception);
            }
        }

        public ValueTask DisposeAsync()
        {
            _stream.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
