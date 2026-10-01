using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public interface IPageObserverManagementStore
{
    Task<PageObserverDefinition?> GetDefinitionAsync(
        Guid observerId,
        CancellationToken cancellationToken = default);

    Task SaveDefinitionAsync(
        PageObserverDefinition definition,
        bool resetSnapshot,
        CancellationToken cancellationToken = default);
}

public interface IPageObserverManagementService
{
    Task<PageObserverDefinition?> GetAsync(
        Guid observerId,
        CancellationToken cancellationToken = default);

    Task<PageObserverSaveResult> SaveAsync(
        PageObserverDefinition definition,
        CancellationToken cancellationToken = default);
}

public sealed record PageObserverSaveResult(
    bool Success,
    IReadOnlyList<string> Errors);

public sealed class PageObserverManagementService(
    IPageObserverManagementStore store) : IPageObserverManagementService
{
    public Task<PageObserverDefinition?> GetAsync(
        Guid observerId,
        CancellationToken cancellationToken = default)
    {
        if (observerId == Guid.Empty)
        {
            throw new ArgumentException(
                "Observer ID must not be empty.",
                nameof(observerId));
        }

        return store.GetDefinitionAsync(
            observerId,
            cancellationToken);
    }

    public async Task<PageObserverSaveResult> SaveAsync(
        PageObserverDefinition definition,
        CancellationToken cancellationToken = default)
    {
        PageObserverDefinition validated;

        try
        {
            validated = PageObserverDefinition.Validate(definition);
        }
        catch (ArgumentException exception)
        {
            return new PageObserverSaveResult(
                false,
                [exception.Message]);
        }

        var existing = await store.GetDefinitionAsync(
            validated.ObserverId,
            cancellationToken);

        await store.SaveDefinitionAsync(
            validated,
            resetSnapshot:
                existing is not null &&
                RequiresSnapshotReset(existing, validated),
            cancellationToken);

        return new PageObserverSaveResult(true, []);
    }

    private static bool RequiresSnapshotReset(
        PageObserverDefinition existing,
        PageObserverDefinition updated) =>
        !string.Equals(existing.Url, updated.Url, StringComparison.Ordinal) ||
        !string.Equals(
            existing.BrowserProfile,
            updated.BrowserProfile,
            StringComparison.Ordinal) ||
        existing.Condition != updated.Condition ||
        existing.Locator != updated.Locator ||
        !string.Equals(
            existing.ExpectedValue,
            updated.ExpectedValue,
            StringComparison.Ordinal) ||
        !string.Equals(
            existing.EventType,
            updated.EventType,
            StringComparison.Ordinal) ||
        !string.Equals(
            existing.CorrelationId,
            updated.CorrelationId,
            StringComparison.Ordinal);
}
