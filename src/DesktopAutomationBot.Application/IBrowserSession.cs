namespace DesktopAutomationBot.Application;

public sealed record BrowserSessionRequest
{
    public string? ProfileName { get; init; }

    public bool? Headless { get; init; }

    public bool IsPersistent => !string.IsNullOrWhiteSpace(ProfileName);
}

public interface IBrowserSession : IBrowserAutomation
{
}

public interface IBrowserSessionFactory
{
    ValueTask<IBrowserSession> CreateAsync(
        CancellationToken cancellationToken = default);

    ValueTask<IBrowserSession> CreateAsync(
        BrowserSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.IsPersistent)
        {
            return ValueTask.FromException<IBrowserSession>(
                new NotSupportedException(
                    "Persistent browser profiles are not supported by this browser session factory."));
        }

        return CreateAsync(cancellationToken);
    }
}
