namespace DesktopAutomationBot.Application;

public interface IBrowserProfileManagementService
{
    Task CreateAsync(
        string profileName,
        CancellationToken cancellationToken = default);

    Task RenameAsync(
        string profileName,
        string newProfileName,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string profileName,
        CancellationToken cancellationToken = default);
}

public static class BrowserProfileNameRules
{
    public static void Validate(string profileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

        if (profileName.Length > 64 ||
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
}
