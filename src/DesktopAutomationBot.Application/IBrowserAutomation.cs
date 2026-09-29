using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public interface IBrowserAutomation : IAsyncDisposable
{
    Task OpenAsync(CancellationToken cancellationToken = default);

    Task NavigateAsync(string url, int? timeoutMs = null, CancellationToken cancellationToken = default);

    Task ClickAsync(string selector, int? timeoutMs = null, CancellationToken cancellationToken = default);

    Task ClickAsync(
        ScenarioLocator locator,
        int? timeoutMs = null,
        CancellationToken cancellationToken = default) =>
        locator.Kind == ScenarioLocatorKind.Selector
            ? ClickAsync(locator.Value, timeoutMs, cancellationToken)
            : Task.FromException(
                new NotSupportedException(
                    $"Locator kind '{locator.Kind}' is not supported by this browser automation implementation."));

    Task FillTextAsync(string selector, string value, int? timeoutMs = null, CancellationToken cancellationToken = default);

    Task FillTextAsync(
        ScenarioLocator locator,
        string value,
        int? timeoutMs = null,
        CancellationToken cancellationToken = default) =>
        locator.Kind == ScenarioLocatorKind.Selector
            ? FillTextAsync(locator.Value, value, timeoutMs, cancellationToken)
            : Task.FromException(
                new NotSupportedException(
                    $"Locator kind '{locator.Kind}' is not supported by this browser automation implementation."));

    Task PasteTextAsync(string selector, string value, int? timeoutMs = null, CancellationToken cancellationToken = default);

    Task PasteTextAsync(
        ScenarioLocator locator,
        string value,
        int? timeoutMs = null,
        CancellationToken cancellationToken = default) =>
        locator.Kind == ScenarioLocatorKind.Selector
            ? PasteTextAsync(locator.Value, value, timeoutMs, cancellationToken)
            : Task.FromException(
                new NotSupportedException(
                    $"Locator kind '{locator.Kind}' is not supported by this browser automation implementation."));

    Task<string> ReadTextAsync(string selector, int? timeoutMs = null, CancellationToken cancellationToken = default);

    Task<string> ReadTextAsync(
        ScenarioLocator locator,
        int? timeoutMs = null,
        CancellationToken cancellationToken = default) =>
        locator.Kind == ScenarioLocatorKind.Selector
            ? ReadTextAsync(locator.Value, timeoutMs, cancellationToken)
            : Task.FromException<string>(
                new NotSupportedException(
                    $"Locator kind '{locator.Kind}' is not supported by this browser automation implementation."));

    Task WaitForSelectorAsync(string selector, int? timeoutMs = null, CancellationToken cancellationToken = default);

    Task WaitForLocatorAsync(
        ScenarioLocator locator,
        int? timeoutMs = null,
        CancellationToken cancellationToken = default) =>
        locator.Kind == ScenarioLocatorKind.Selector
            ? WaitForSelectorAsync(locator.Value, timeoutMs, cancellationToken)
            : Task.FromException(
                new NotSupportedException(
                    $"Locator kind '{locator.Kind}' is not supported by this browser automation implementation."));

    Task WaitForTextAsync(string text, int? timeoutMs = null, CancellationToken cancellationToken = default);

    Task WaitForUrlAsync(string url, int? timeoutMs = null, CancellationToken cancellationToken = default);

    Task WaitForLoadStateAsync(string loadState, int? timeoutMs = null, CancellationToken cancellationToken = default);

    Task<string> TakeScreenshotAsync(string filePath, CancellationToken cancellationToken = default);

    Task<string> SaveHtmlSnapshotAsync(
        string filePath,
        CancellationToken cancellationToken = default) =>
        Task.FromException<string>(
            new NotSupportedException("HTML snapshots are not supported by this browser automation implementation."));
}
