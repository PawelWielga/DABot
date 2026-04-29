namespace DesktopAutomationBot.Application;

public interface IBrowserAutomation : IAsyncDisposable
{
    Task OpenAsync(CancellationToken cancellationToken = default);

    Task NavigateAsync(string url, int? timeoutMs = null, CancellationToken cancellationToken = default);

    Task ClickAsync(string selector, int? timeoutMs = null, CancellationToken cancellationToken = default);

    Task FillTextAsync(string selector, string value, int? timeoutMs = null, CancellationToken cancellationToken = default);

    Task PasteTextAsync(string selector, string value, int? timeoutMs = null, CancellationToken cancellationToken = default);

    Task<string> ReadTextAsync(string selector, int? timeoutMs = null, CancellationToken cancellationToken = default);

    Task WaitForSelectorAsync(string selector, int? timeoutMs = null, CancellationToken cancellationToken = default);

    Task WaitForTextAsync(string text, int? timeoutMs = null, CancellationToken cancellationToken = default);

    Task WaitForUrlAsync(string url, int? timeoutMs = null, CancellationToken cancellationToken = default);

    Task WaitForLoadStateAsync(string loadState, int? timeoutMs = null, CancellationToken cancellationToken = default);

    Task<string> TakeScreenshotAsync(string filePath, CancellationToken cancellationToken = default);
}
