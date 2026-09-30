using DesktopAutomationBot.Application;

namespace DesktopAutomationBot.Infrastructure;

public sealed class FileSystemBrowserProfileManagementService(
    BotOptions options) : IBrowserProfileManagementService
{
    public Task CreateAsync(
        string profileName,
        CancellationToken cancellationToken = default)
    {
        BrowserProfileNameRules.Validate(profileName);
        cancellationToken.ThrowIfCancellationRequested();

        var paths = GetPaths(profileName);
        Directory.CreateDirectory(paths.Root);
        Directory.CreateDirectory(paths.Locks);

        using var lease = AcquireLease(paths.Locks, profileName);

        if (Directory.Exists(paths.Profile))
        {
            throw new InvalidOperationException(
                $"Browser profile '{profileName}' already exists.");
        }

        Directory.CreateDirectory(paths.Profile);
        return Task.CompletedTask;
    }

    public Task RenameAsync(
        string profileName,
        string newProfileName,
        CancellationToken cancellationToken = default)
    {
        BrowserProfileNameRules.Validate(profileName);
        BrowserProfileNameRules.Validate(newProfileName);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.Equals(profileName, newProfileName, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var source = GetPaths(profileName);
        var target = GetPaths(newProfileName);
        Directory.CreateDirectory(source.Root);
        Directory.CreateDirectory(source.Locks);

        var lockNames = new[] { profileName, newProfileName }
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        using var firstLease = AcquireLease(source.Locks, lockNames[0]);
        using var secondLease = AcquireLease(source.Locks, lockNames[1]);

        EnsureProfileExists(source.Profile, profileName);
        EnsureNotReparsePoint(source.Profile, profileName);

        if (Directory.Exists(target.Profile))
        {
            throw new InvalidOperationException(
                $"Browser profile '{newProfileName}' already exists.");
        }

        Directory.Move(source.Profile, target.Profile);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(
        string profileName,
        CancellationToken cancellationToken = default)
    {
        BrowserProfileNameRules.Validate(profileName);
        cancellationToken.ThrowIfCancellationRequested();

        var paths = GetPaths(profileName);
        Directory.CreateDirectory(paths.Root);
        Directory.CreateDirectory(paths.Locks);

        using var lease = AcquireLease(paths.Locks, profileName);

        EnsureProfileExists(paths.Profile, profileName);
        EnsureNotReparsePoint(paths.Profile, profileName);

        Directory.Delete(paths.Profile, recursive: true);
        return Task.CompletedTask;
    }

    private ProfilePaths GetPaths(string profileName)
    {
        var root = Path.GetFullPath(options.Storage.BrowserProfilesDirectory);

        return new ProfilePaths(
            root,
            Path.Combine(root, profileName),
            Path.Combine(root, ".locks"));
    }

    private static BrowserProfileLease AcquireLease(
        string locksDirectory,
        string profileName) =>
        BrowserProfileLease.Acquire(
            Path.Combine(
                locksDirectory,
                $"{profileName}.lock"));

    private static void EnsureProfileExists(
        string profileDirectory,
        string profileName)
    {
        if (!Directory.Exists(profileDirectory))
        {
            throw new InvalidOperationException(
                $"Browser profile '{profileName}' does not exist.");
        }
    }

    private static void EnsureNotReparsePoint(
        string profileDirectory,
        string profileName)
    {
        var attributes = File.GetAttributes(profileDirectory);

        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                $"Browser profile '{profileName}' points to a symbolic link or reparse point and cannot be modified.");
        }
    }

    private sealed record ProfilePaths(
        string Root,
        string Profile,
        string Locks);
}
