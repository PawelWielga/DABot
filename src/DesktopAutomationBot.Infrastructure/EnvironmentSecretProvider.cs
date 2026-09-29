using DesktopAutomationBot.Application;

namespace DesktopAutomationBot.Infrastructure;

public sealed class EnvironmentSecretProvider : ISecretProvider
{
    public ValueTask<string?> GetSecretAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(
            Environment.GetEnvironmentVariable(name));
    }
}
