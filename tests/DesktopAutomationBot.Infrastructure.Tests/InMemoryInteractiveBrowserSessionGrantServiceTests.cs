using DesktopAutomationBot.Application;
using DesktopAutomationBot.Infrastructure;
using FluentAssertions;

namespace DesktopAutomationBot.Infrastructure.Tests;

public sealed class InMemoryInteractiveBrowserSessionGrantServiceTests
{
    [Fact]
    public async Task IssueAndConsumeAsync_ForActiveSession_IsSingleUse()
    {
        var now = new DateTimeOffset(2026, 9, 30, 20, 0, 0, TimeSpan.Zero);
        var sessionId = Guid.NewGuid();
        var sessions = new RecordingInteractiveSessionService(
            CreateSession(sessionId, now));
        var time = new MutableTimeProvider(now);
        var service = CreateService(sessions, time);

        var grant = await service.IssueAsync(sessionId);

        grant.SessionId.Should().Be(sessionId);
        grant.AccessToken.Should().NotBeNullOrWhiteSpace();
        grant.AccessToken.Length.Should().BeGreaterThan(40);
        grant.ExpiresAt.Should().Be(now.AddSeconds(60));

        (await service.ConsumeAsync(
            sessionId,
            grant.AccessToken)).Should().BeTrue();

        (await service.ConsumeAsync(
            sessionId,
            grant.AccessToken)).Should().BeFalse();
    }

    [Fact]
    public async Task IssueAsync_RotatesPreviousOutstandingGrantForSession()
    {
        var now = DateTimeOffset.UtcNow;
        var sessionId = Guid.NewGuid();
        var sessions = new RecordingInteractiveSessionService(
            CreateSession(sessionId, now));
        var service = CreateService(
            sessions,
            new MutableTimeProvider(now));

        var first = await service.IssueAsync(sessionId);
        var second = await service.IssueAsync(sessionId);

        first.AccessToken.Should().NotBe(second.AccessToken);
        (await service.ConsumeAsync(
            sessionId,
            first.AccessToken)).Should().BeFalse();
        (await service.ConsumeAsync(
            sessionId,
            second.AccessToken)).Should().BeTrue();
    }

    [Fact]
    public async Task IssueAsync_WhenSessionIsNotActive_RejectsGrant()
    {
        var service = CreateService(
            new RecordingInteractiveSessionService(),
            new MutableTimeProvider(DateTimeOffset.UtcNow));

        Func<Task> issue = async () =>
            await service.IssueAsync(Guid.NewGuid());

        await issue.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*is not active*");
    }

    [Fact]
    public async Task ConsumeAsync_AfterGrantExpires_ReturnsFalse()
    {
        var now = DateTimeOffset.UtcNow;
        var sessionId = Guid.NewGuid();
        var sessions = new RecordingInteractiveSessionService(
            CreateSession(sessionId, now));
        var time = new MutableTimeProvider(now);
        var service = CreateService(
            sessions,
            time,
            grantLifetimeSeconds: 2);

        var grant = await service.IssueAsync(sessionId);
        time.Advance(TimeSpan.FromSeconds(3));

        (await service.ConsumeAsync(
            sessionId,
            grant.AccessToken)).Should().BeFalse();
    }

    [Fact]
    public async Task ConsumeAsync_WhenSessionEnded_ReturnsFalse()
    {
        var now = DateTimeOffset.UtcNow;
        var sessionId = Guid.NewGuid();
        var sessions = new RecordingInteractiveSessionService(
            CreateSession(sessionId, now));
        var service = CreateService(
            sessions,
            new MutableTimeProvider(now));

        var grant = await service.IssueAsync(sessionId);
        sessions.ActiveSessions.Clear();

        (await service.ConsumeAsync(
            sessionId,
            grant.AccessToken)).Should().BeFalse();
    }

    [Fact]
    public async Task ConsumeAsync_WithWrongSession_DoesNotConsumeValidGrant()
    {
        var now = DateTimeOffset.UtcNow;
        var firstSessionId = Guid.NewGuid();
        var secondSessionId = Guid.NewGuid();
        var sessions = new RecordingInteractiveSessionService(
            CreateSession(firstSessionId, now),
            CreateSession(secondSessionId, now));
        var service = CreateService(
            sessions,
            new MutableTimeProvider(now));

        var grant = await service.IssueAsync(firstSessionId);

        (await service.ConsumeAsync(
            secondSessionId,
            grant.AccessToken)).Should().BeFalse();

        (await service.ConsumeAsync(
            firstSessionId,
            grant.AccessToken)).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(301)]
    public async Task IssueAsync_WhenGrantLifetimeIsUnsafe_RejectsConfiguration(
        int grantLifetimeSeconds)
    {
        var now = DateTimeOffset.UtcNow;
        var sessionId = Guid.NewGuid();
        var sessions = new RecordingInteractiveSessionService(
            CreateSession(sessionId, now));
        var service = CreateService(
            sessions,
            new MutableTimeProvider(now),
            grantLifetimeSeconds);

        Func<Task> issue = async () =>
            await service.IssueAsync(sessionId);

        await issue.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*grantLifetimeSeconds must be between 1 and 300*");
    }

    private static InMemoryInteractiveBrowserSessionGrantService CreateService(
        RecordingInteractiveSessionService sessions,
        TimeProvider timeProvider,
        int grantLifetimeSeconds = 60) =>
        new(
            sessions,
            new BotOptions
            {
                InteractiveBrowser = new InteractiveBrowserOptions
                {
                    GrantLifetimeSeconds = grantLifetimeSeconds,
                },
            },
            timeProvider);

    private static InteractiveBrowserSessionInfo CreateSession(
        Guid sessionId,
        DateTimeOffset now) =>
        new()
        {
            SessionId = sessionId,
            ProfileName = $"profile-{sessionId:N}",
            StartedAt = now,
            ExpiresAt = now.AddMinutes(30),
        };

    private sealed class RecordingInteractiveSessionService(
        params InteractiveBrowserSessionInfo[] sessions) :
        IInteractiveBrowserSessionService
    {
        public List<InteractiveBrowserSessionInfo> ActiveSessions { get; } =
            [.. sessions];

        public Task<IReadOnlyList<InteractiveBrowserSessionInfo>> ListAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<InteractiveBrowserSessionInfo>>(
                ActiveSessions.ToArray());
        }

        public Task<InteractiveBrowserSessionInfo> StartAsync(
            string profileName,
            string? url = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> StopAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class MutableTimeProvider(
        DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() =>
            _utcNow;

        public void Advance(TimeSpan duration) =>
            _utcNow = _utcNow.Add(duration);
    }
}
