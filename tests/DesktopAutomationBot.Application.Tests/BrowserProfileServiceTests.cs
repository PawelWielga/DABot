using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class BrowserProfileServiceTests
{
    [Fact]
    public async Task OpenInteractiveAsync_RequestsHeadedPersistentSessionAndNavigates()
    {
        var factory = new RecordingFactory();
        var service = new BrowserProfileService(factory);

        await using var session = await service.OpenInteractiveAsync(
            "work-profile",
            "https://example.com");

        factory.LastRequest.Should().NotBeNull();
        factory.LastRequest!.ProfileName.Should().Be("work-profile");
        factory.LastRequest.Headless.Should().BeFalse();
        factory.Session.OpenCount.Should().Be(1);
        factory.Session.NavigatedUrls.Should().Equal("https://example.com");
        factory.Session.DisposeCount.Should().Be(0);
    }

    [Fact]
    public async Task TestAsync_RequestsHeadlessPersistentSessionAndDisposesIt()
    {
        var factory = new RecordingFactory();
        var service = new BrowserProfileService(factory);

        var result = await service.TestAsync("health-profile");

        result.Success.Should().BeTrue();
        result.ProfileName.Should().Be("health-profile");
        factory.LastRequest.Should().NotBeNull();
        factory.LastRequest!.ProfileName.Should().Be("health-profile");
        factory.LastRequest.Headless.Should().BeTrue();
        factory.Session.OpenCount.Should().Be(1);
        factory.Session.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task TestAsync_WhenSessionCannotOpen_ReturnsFailure()
    {
        var factory = new RecordingFactory
        {
            ThrowOnOpen = true,
        };
        var service = new BrowserProfileService(factory);

        var result = await service.TestAsync("broken-profile");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Profile open failed.");
        factory.Session.DisposeCount.Should().Be(1);
    }

    private sealed class RecordingFactory : IBrowserSessionFactory
    {
        public BrowserSessionRequest? LastRequest { get; private set; }

        public RecordingSession Session { get; } = new();

        public bool ThrowOnOpen
        {
            set => Session.ThrowOnOpen = value;
        }

        public ValueTask<IBrowserSession> CreateAsync(
            CancellationToken cancellationToken = default) =>
            CreateAsync(
                new BrowserSessionRequest(),
                cancellationToken);

        public ValueTask<IBrowserSession> CreateAsync(
            BrowserSessionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return ValueTask.FromResult<IBrowserSession>(Session);
        }
    }

    private sealed class RecordingSession : IInteractiveBrowserSession
    {
        private readonly TaskCompletionSource _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Completion => _completion.Task;

        public bool ThrowOnOpen { get; set; }

        public int OpenCount { get; private set; }

        public int DisposeCount { get; private set; }

        public List<string> NavigatedUrls { get; } = [];

        public Task OpenAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCount++;

            if (ThrowOnOpen)
            {
                throw new InvalidOperationException("Profile open failed.");
            }

            return Task.CompletedTask;
        }

        public Task NavigateAsync(
            string url,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default)
        {
            NavigatedUrls.Add(url);
            return Task.CompletedTask;
        }

        public Task ClickAsync(string selector, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task FillTextAsync(string selector, string value, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PasteTextAsync(string selector, string value, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string> ReadTextAsync(string selector, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
        public Task WaitForSelectorAsync(string selector, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task WaitForTextAsync(string text, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task WaitForUrlAsync(string url, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task WaitForLoadStateAsync(string loadState, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string> TakeScreenshotAsync(string filePath, CancellationToken cancellationToken = default) => Task.FromResult(filePath);

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            _completion.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }
}
