namespace DesktopAutomationBot.Application;

internal sealed class FixedBrowserSessionFactory(
    IBrowserAutomation automation) : IBrowserSessionFactory
{
    public ValueTask<IBrowserSession> CreateAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IBrowserSession>(
            automation as IBrowserSession ??
            new DelegatingBrowserSession(automation));
    }

    private sealed class DelegatingBrowserSession(
        IBrowserAutomation inner) : IBrowserSession
    {
        public Task OpenAsync(CancellationToken cancellationToken = default) =>
            inner.OpenAsync(cancellationToken);

        public Task NavigateAsync(
            string url,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            inner.NavigateAsync(url, timeoutMs, cancellationToken);

        public Task ClickAsync(
            string selector,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            inner.ClickAsync(selector, timeoutMs, cancellationToken);

        public Task ClickAsync(
            Core.ScenarioLocator locator,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            inner.ClickAsync(locator, timeoutMs, cancellationToken);

        public Task FillTextAsync(
            string selector,
            string value,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            inner.FillTextAsync(selector, value, timeoutMs, cancellationToken);

        public Task FillTextAsync(
            Core.ScenarioLocator locator,
            string value,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            inner.FillTextAsync(locator, value, timeoutMs, cancellationToken);

        public Task PasteTextAsync(
            string selector,
            string value,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            inner.PasteTextAsync(selector, value, timeoutMs, cancellationToken);

        public Task PasteTextAsync(
            Core.ScenarioLocator locator,
            string value,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            inner.PasteTextAsync(locator, value, timeoutMs, cancellationToken);

        public Task<string> ReadTextAsync(
            string selector,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            inner.ReadTextAsync(selector, timeoutMs, cancellationToken);

        public Task<string> ReadTextAsync(
            Core.ScenarioLocator locator,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            inner.ReadTextAsync(locator, timeoutMs, cancellationToken);

        public Task WaitForSelectorAsync(
            string selector,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            inner.WaitForSelectorAsync(selector, timeoutMs, cancellationToken);

        public Task WaitForLocatorAsync(
            Core.ScenarioLocator locator,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            inner.WaitForLocatorAsync(locator, timeoutMs, cancellationToken);

        public Task WaitForTextAsync(
            string text,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            inner.WaitForTextAsync(text, timeoutMs, cancellationToken);

        public Task WaitForUrlAsync(
            string url,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            inner.WaitForUrlAsync(url, timeoutMs, cancellationToken);

        public Task WaitForLoadStateAsync(
            string loadState,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            inner.WaitForLoadStateAsync(loadState, timeoutMs, cancellationToken);

        public Task<string> TakeScreenshotAsync(
            string filePath,
            CancellationToken cancellationToken = default) =>
            inner.TakeScreenshotAsync(filePath, cancellationToken);

        public Task<string> SaveHtmlSnapshotAsync(
            string filePath,
            CancellationToken cancellationToken = default) =>
            inner.SaveHtmlSnapshotAsync(filePath, cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
