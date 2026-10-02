namespace DesktopAutomationBot.Application;

public sealed record NodeIdentity
{
    public required Guid NodeId { get; init; }
}

public interface INodeIdentityProvider
{
    Task<NodeIdentity> GetAsync(
        CancellationToken cancellationToken = default);
}
