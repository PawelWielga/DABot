namespace DesktopAutomationBot.Application;

public sealed record BrowserProfileTestResult
{
    public required string ProfileName { get; init; }

    public bool Success { get; init; }

    public string? ErrorMessage { get; init; }
}

public interface IBrowserProfileService
{
    ValueTask<IInteractiveBrowserSession> OpenInteractiveAsync(
        string profileName,
        string? url = null,
        CancellationToken cancellationToken = default);

    Task<BrowserProfileTestResult> TestAsync(
        string profileName,
        CancellationToken cancellationToken = default);
}

public sealed class BrowserProfileService(
    IBrowserSessionFactory sessionFactory) : IBrowserProfileService
{
    public async ValueTask<IInteractiveBrowserSession> OpenInteractiveAsync(
        string profileName,
        string? url = null,
        CancellationToken cancellationToken = default)
    {
        var session = await sessionFactory.CreateAsync(
            new BrowserSessionRequest
            {
                ProfileName = profileName,
                Headless = false,
            },
            cancellationToken);

        if (session is not IInteractiveBrowserSession interactiveSession)
        {
            await session.DisposeAsync();
            throw new NotSupportedException(
                "The configured browser session does not expose interactive-session lifetime events.");
        }

        try
        {
            await interactiveSession.OpenAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(url))
            {
                await interactiveSession.NavigateAsync(
                    url,
                    cancellationToken: cancellationToken);
            }

            return interactiveSession;
        }
        catch
        {
            await interactiveSession.DisposeAsync();
            throw;
        }
    }

    public async Task<BrowserProfileTestResult> TestAsync(
        string profileName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var session = await sessionFactory.CreateAsync(
                new BrowserSessionRequest
                {
                    ProfileName = profileName,
                    Headless = true,
                },
                cancellationToken);

            await session.OpenAsync(cancellationToken);

            return new BrowserProfileTestResult
            {
                ProfileName = profileName,
                Success = true,
            };
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            return new BrowserProfileTestResult
            {
                ProfileName = profileName,
                Success = false,
                ErrorMessage = exception.Message,
            };
        }
    }
}
