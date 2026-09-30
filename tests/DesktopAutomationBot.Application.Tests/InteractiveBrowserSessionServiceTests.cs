using DesktopAutomationBot.Application;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class InteractiveBrowserSessionServiceTests
{
    [Fact]
    public async Task StartAsync_KeepsSessionActiveUntilStopped()
    {
        var profiles = new RecordingProfileService();
        await using var service = CreateService(profiles);

        var started = await service.StartAsync("work-profile");

        started.ProfileName.Should().Be("work-profile");
        started.SessionId.Should().NotBeEmpty();
        started.ExpiresAt.Should().BeAfter(started.StartedAt);
        profiles.OpenedProfiles.Should().Equal("work-profile");
        profiles.Sessions.Should().ContainSingle();
        profiles.Sessions[0].DisposeCount.Should().Be(0);

        var active = await service.ListAsync();
        active.Should().ContainSingle();
        active[0].SessionId.Should().Be(started.SessionId);

        (await service.StopAsync(started.SessionId)).Should().BeTrue();
        profiles.Sessions[0].DisposeCount.Should().Be(1);
        (await service.ListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task StartAsync_WhenProfileAlreadyInteractive_RejectsDuplicateSession()
    {
        var profiles = new RecordingProfileService();
        await using var service = CreateService(profiles);

        await service.StartAsync("work-profile");

        Func<Task> duplicate = async () =>
            await service.StartAsync("work-profile");

        await duplicate.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*already has an active interactive session*");

        profiles.OpenedProfiles.Should().Equal("work-profile");
    }

    [Fact]
    public async Task StartAsync_WhenMaxDurationIsInvalid_RejectsBeforeOpeningBrowser()
    {
        var profiles = new RecordingProfileService();
        await using var service = CreateService(
            profiles,
            maxDurationSeconds: 0);

        Func<Task> start = async () =>
            await service.StartAsync("invalid-timeout-profile");

        await start.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*maxDurationSeconds must be at least 1*");

        profiles.Sessions.Should().BeEmpty();
    }

    [Fact]
    public async Task MaxDuration_ExpiresSessionAndDisposesHandle()
    {
        var profiles = new RecordingProfileService();
        await using var service = CreateService(
            profiles,
            maxDurationSeconds: 1);

        var started = await service.StartAsync("expiring-profile");
        var session = profiles.Sessions.Single();

        await WaitUntilAsync(
            async () =>
                (await service.ListAsync()).Count == 0 &&
                session.DisposeCount == 1);

        session.DisposeCount.Should().Be(1);
        (await service.StopAsync(started.SessionId)).Should().BeFalse();
    }

    [Fact]
    public async Task BrowserCompletion_RemovesSessionAndDisposesHandle()
    {
        var profiles = new RecordingProfileService();
        await using var service = CreateService(profiles);

        var started = await service.StartAsync("work-profile");
        var session = profiles.Sessions.Single();

        session.SignalBrowserClosed();

        await WaitUntilAsync(
            async () =>
                (await service.ListAsync()).Count == 0 &&
                session.DisposeCount == 1);

        session.DisposeCount.Should().Be(1);
        (await service.StopAsync(started.SessionId)).Should().BeFalse();
    }

    private static InteractiveBrowserSessionService CreateService(
        RecordingProfileService profiles,
        int maxDurationSeconds = 1800) =>
        new(
            profiles,
            TimeProvider.System,
            new BotOptions
            {
                InteractiveBrowser = new InteractiveBrowserOptions
                {
                    MaxDurationSeconds = maxDurationSeconds,
                },
            });

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);

        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("Condition was not reached before the test timeout.");
    }

    private sealed class RecordingProfileService : IBrowserProfileService
    {
        public List<string> OpenedProfiles { get; } = [];

        public List<RecordingInteractiveSession> Sessions { get; } = [];

        public ValueTask<IInteractiveBrowserSession> OpenInteractiveAsync(
            string profileName,
            string? url = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var session = new RecordingInteractiveSession();
            OpenedProfiles.Add(profileName);
            Sessions.Add(session);
            return ValueTask.FromResult<IInteractiveBrowserSession>(session);
        }

        public Task<BrowserProfileTestResult> TestAsync(
            string profileName,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingInteractiveSession : IInteractiveBrowserSession
    {
        private readonly TaskCompletionSource<bool> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Completion => _completion.Task;

        public int DisposeCount { get; private set; }

        public void SignalBrowserClosed() =>
            _completion.TrySetResult(true);

        public Task OpenAsync(
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task NavigateAsync(
            string url,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

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
            _completion.TrySetResult(true);
            return ValueTask.CompletedTask;
        }
    }
}
