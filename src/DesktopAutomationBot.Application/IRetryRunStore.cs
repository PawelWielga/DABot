namespace DesktopAutomationBot.Application;

public interface IRetryRunStore
{
    Task<IReadOnlyList<Guid>> LoadDueRetryRunIdsAsync(
        DateTimeOffset dueAt,
        int limit = 100,
        CancellationToken cancellationToken = default);
}
