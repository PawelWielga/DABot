namespace DesktopAutomationBot.Application;

public sealed record InteractiveBrowserSessionGrant
{
    public required Guid SessionId { get; init; }

    public required string AccessToken { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}

public interface IInteractiveBrowserSessionGrantService
{
    Task<InteractiveBrowserSessionGrant> IssueAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<bool> ConsumeAsync(
        Guid sessionId,
        string accessToken,
        CancellationToken cancellationToken = default);
}
