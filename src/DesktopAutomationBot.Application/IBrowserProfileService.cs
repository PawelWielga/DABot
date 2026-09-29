namespace DesktopAutomationBot.Application;

public sealed record BrowserProfileTestResult
{
    public required string ProfileName { get; init; }

    public bool Success { get; init; }

    public string? ErrorMessage { get; init; }
}

public interface IBrowserProfileService
{
    ValueTask<IBrowserSession> OpenInteractiveAsync(
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
    public async ValueTask<IBrowserSession> OpenInteractiveAsync(
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

        try
        {
            await session.OpenAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(url))
            {
                await session.NavigateAsync(
                    url,
                    cancellationToken: cancellationToken);
            }

            return session;
        }
        catch
        {
            await session.DisposeAsync();
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
