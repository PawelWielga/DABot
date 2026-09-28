using DesktopAutomationBot.Application;
using Microsoft.Playwright;

namespace DesktopAutomationBot.Infrastructure;

public sealed class PlaywrightBrowserAutomation : IBrowserAutomation
{
    private readonly BotOptions _options;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;

    public PlaywrightBrowserAutomation(BotOptions options)
    {
        _options = options;
    }

    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_page is not null)
        {
            return;
        }

        _playwright = await Playwright.CreateAsync()
            .WaitAsync(cancellationToken);

        var launchOptions = new BrowserTypeLaunchOptions
        {
            Headless = _options.Browser.Headless,
        };

        if (_options.Browser.SlowMoMs > 0)
        {
            launchOptions.SlowMo = _options.Browser.SlowMoMs;
        }

        _browser = await _playwright.Chromium.LaunchAsync(launchOptions)
            .WaitAsync(cancellationToken);
        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = _options.Browser.ViewportWidth,
                Height = _options.Browser.ViewportHeight,
            },
        }).WaitAsync(cancellationToken);

        _page = await _context.NewPageAsync()
            .WaitAsync(cancellationToken);
        ApplyTimeouts(_page);
    }

    public async Task NavigateAsync(string url, int? timeoutMs = null, CancellationToken cancellationToken = default)
    {
        var page = await GetPageAsync(cancellationToken);
        await page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = GetTimeout(timeoutMs),
        }).WaitAsync(cancellationToken);
    }

    public async Task ClickAsync(string selector, int? timeoutMs = null, CancellationToken cancellationToken = default)
    {
        var page = await GetPageAsync(cancellationToken);
        await page.Locator(selector).ClickAsync(new LocatorClickOptions
        {
            Timeout = GetTimeout(timeoutMs),
        }).WaitAsync(cancellationToken);
    }

    public async Task FillTextAsync(string selector, string value, int? timeoutMs = null, CancellationToken cancellationToken = default)
    {
        var page = await GetPageAsync(cancellationToken);
        await page.Locator(selector).FillAsync(value, new LocatorFillOptions
        {
            Timeout = GetTimeout(timeoutMs),
        }).WaitAsync(cancellationToken);
    }

    public async Task PasteTextAsync(string selector, string value, int? timeoutMs = null, CancellationToken cancellationToken = default)
    {
        var page = await GetPageAsync(cancellationToken);
        var locator = page.Locator(selector);
        await locator.ClickAsync(new LocatorClickOptions
        {
            Timeout = GetTimeout(timeoutMs),
        }).WaitAsync(cancellationToken);
        await locator.PressAsync("Control+A")
            .WaitAsync(cancellationToken);
        await page.Keyboard.InsertTextAsync(value)
            .WaitAsync(cancellationToken);
    }

    public async Task<string> ReadTextAsync(string selector, int? timeoutMs = null, CancellationToken cancellationToken = default)
    {
        var page = await GetPageAsync(cancellationToken);
        return await page.Locator(selector).InnerTextAsync(new LocatorInnerTextOptions
        {
            Timeout = GetTimeout(timeoutMs),
        }).WaitAsync(cancellationToken);
    }

    public async Task WaitForSelectorAsync(string selector, int? timeoutMs = null, CancellationToken cancellationToken = default)
    {
        var page = await GetPageAsync(cancellationToken);
        await page.Locator(selector).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = GetTimeout(timeoutMs),
        }).WaitAsync(cancellationToken);
    }

    public async Task WaitForTextAsync(string text, int? timeoutMs = null, CancellationToken cancellationToken = default)
    {
        var page = await GetPageAsync(cancellationToken);
        await page.GetByText(text).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = GetTimeout(timeoutMs),
        }).WaitAsync(cancellationToken);
    }

    public async Task WaitForUrlAsync(string url, int? timeoutMs = null, CancellationToken cancellationToken = default)
    {
        var page = await GetPageAsync(cancellationToken);
        await page.WaitForURLAsync(url, new PageWaitForURLOptions
        {
            Timeout = GetTimeout(timeoutMs),
        }).WaitAsync(cancellationToken);
    }

    public async Task WaitForLoadStateAsync(string loadState, int? timeoutMs = null, CancellationToken cancellationToken = default)
    {
        var page = await GetPageAsync(cancellationToken);
        await page.WaitForLoadStateAsync(ParseLoadState(loadState), new PageWaitForLoadStateOptions
        {
            Timeout = GetTimeout(timeoutMs),
        }).WaitAsync(cancellationToken);
    }

    public async Task<string> TakeScreenshotAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var page = await GetPageAsync(cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? ".");
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            FullPage = true,
            Path = filePath,
        }).WaitAsync(cancellationToken);
        return filePath;
    }

    public async ValueTask DisposeAsync()
    {
        if (_page is not null)
        {
            await _page.CloseAsync();
        }

        if (_context is not null)
        {
            await _context.CloseAsync();
        }

        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();
        _page = null;
        _context = null;
        _browser = null;
        _playwright = null;
    }

    private async Task<IPage> GetPageAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_page is null)
        {
            await OpenAsync(cancellationToken);
        }

        return _page!;
    }

    private void ApplyTimeouts(IPage page)
    {
        page.SetDefaultTimeout(_options.Browser.TimeoutMs);
        page.SetDefaultNavigationTimeout(_options.Browser.TimeoutMs);
    }

    private float GetTimeout(int? timeoutMs) => timeoutMs ?? _options.Browser.TimeoutMs;

    private static LoadState ParseLoadState(string loadState) =>
        loadState.Trim().ToLowerInvariant() switch
        {
            "domcontentloaded" => LoadState.DOMContentLoaded,
            "networkidle" => LoadState.NetworkIdle,
            "load" => LoadState.Load,
            _ => throw new ArgumentOutOfRangeException(nameof(loadState), loadState, "Unsupported load state."),
        };
}
