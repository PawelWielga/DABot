namespace DesktopAutomationBot.Application;

public interface ISecretProvider
{
    ValueTask<string?> GetSecretAsync(
        string name,
        CancellationToken cancellationToken = default);
}
