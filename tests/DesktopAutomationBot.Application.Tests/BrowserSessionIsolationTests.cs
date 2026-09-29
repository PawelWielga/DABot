using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class BrowserSessionIsolationTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(
            Path.GetTempPath(),
            "dabot-browser-session-tests",
            Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ExecuteAsync_ConcurrentRunsUseIndependentOwnedSessions()
    {
        var factory = new RecordingBrowserSessionFactory();
        var executor = new ScenarioExecutor(
            new BotOptions
            {
                Storage = new StorageOptions
                {
                    ScreenshotsDirectory = Path.Combine(
                        _tempDirectory,
                        "screenshots"),
                    ArtifactsDirectory = Path.Combine(
                        _tempDirectory,
                        "artifacts"),
                },
            },
            new ScenarioValidationService(),
            [new OpenUrlStepHandler()],
            factory);

        var firstTask = executor.ExecuteAsync(
            CreateScenario(
                "first-run",
                "https://first.example"));
        var secondTask = executor.ExecuteAsync(
            CreateScenario(
                "second-run",
                "https://second.example"));

        var results = await Task.WhenAll(
            firstTask,
            secondTask);

        results.Should().OnlyContain(result => result.Success);
        factory.Sessions.Should().HaveCount(2);
        factory.Sessions[0].Should().NotBeSameAs(factory.Sessions[1]);

        factory.Sessions
            .SelectMany(session => session.NavigatedUrls)
            .Should()
            .BeEquivalentTo(
                "https://first.example",
                "https://second.example");

        factory.Sessions.Should().OnlyContain(
            session =>
                session.OpenCount == 1 &&
                session.DisposeCount == 1 &&
                session.NavigatedUrls.Count == 1);
    }


    [Fact]
    public async Task ExecuteAsync_WithBrowserProfile_PassesNamedProfileToFactory()
    {
        var factory = new RecordingBrowserSessionFactory();
        var executor = new ScenarioExecutor(
            new BotOptions
            {
                Storage = new StorageOptions
                {
                    ScreenshotsDirectory = Path.Combine(
                        _tempDirectory,
                        "screenshots"),
                    ArtifactsDirectory = Path.Combine(
                        _tempDirectory,
                        "artifacts"),
                },
            },
            new ScenarioValidationService(),
            [new OpenUrlStepHandler()],
            factory);

        var scenario = CreateScenario(
            "profile-run",
            "https://profile.example") with
        {
            BrowserProfile = "work-account",
        };

        var result = await executor.ExecuteAsync(scenario);

        result.Success.Should().BeTrue();
        factory.RequestedProfiles.Should().ContainSingle()
            .Which.Should().Be("work-account");
    }

    private static ScenarioDefinition CreateScenario(
        string name,
        string url) =>
        new()
        {
            Name = name,
            Steps =
            [
                new ScenarioStep
                {
                    Type = StepType.OpenUrl,
                    Url = url,
                },
            ],
        };

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(
                _tempDirectory,
                recursive: true);
        }
    }

    private sealed class RecordingBrowserSessionFactory :
        IBrowserSessionFactory
    {
        private readonly object _sync = new();

        public List<RecordingBrowserSession> Sessions { get; } = [];

        public List<string?> RequestedProfiles { get; } = [];

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

            var session = new RecordingBrowserSession();
            lock (_sync)
            {
                Sessions.Add(session);
                RequestedProfiles.Add(request.ProfileName);
            }

            return ValueTask.FromResult<IBrowserSession>(session);
        }
    }

    private sealed class RecordingBrowserSession : IBrowserSession
    {
        public int OpenCount { get; private set; }

        public int DisposeCount { get; private set; }

        public List<string> NavigatedUrls { get; } = [];

        public Task OpenAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCount++;
            return Task.CompletedTask;
        }

        public Task NavigateAsync(
            string url,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            NavigatedUrls.Add(url);
            return Task.CompletedTask;
        }

        public Task ClickAsync(
            string selector,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task FillTextAsync(
            string selector,
            string value,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PasteTextAsync(
            string selector,
            string value,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string> ReadTextAsync(
            string selector,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Empty);

        public Task WaitForSelectorAsync(
            string selector,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task WaitForTextAsync(
            string text,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task WaitForUrlAsync(
            string url,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task WaitForLoadStateAsync(
            string loadState,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string> TakeScreenshotAsync(
            string filePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(filePath);

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
