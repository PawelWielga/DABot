namespace DesktopAutomationBot.Infrastructure;

internal static class BrowserProfileDirectoryGuard
{
    public static void EnsureNotReparsePoint(
        string profileDirectory,
        string profileName)
    {
        var attributes = File.GetAttributes(profileDirectory);

        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                $"Browser profile '{profileName}' points to a symbolic link or reparse point and cannot be used.");
        }
    }
}
