namespace DesktopAutomationBot.Application;

public sealed record InteractiveBrowserSessionInfo
{
    public required Guid SessionId { get; init; }

    public required string ProfileName { get; init; }

    public string? InitialUrl { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}

public interface IInteractiveBrowserSessionService
{
    Task<IReadOnlyList<InteractiveBrowserSessionInfo>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<InteractiveBrowserSessionInfo> StartAsync(
        string profileName,
        string? url = null,
        CancellationToken cancellationToken = default);

    Task<bool> StopAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);
}

public sealed class InteractiveBrowserSessionService(
    IBrowserProfileService profileService,
    TimeProvider timeProvider,
    BotOptions options) :
    IInteractiveBrowserSessionService,
    IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<Guid, ActiveSession> _sessions = [];

    public async Task<IReadOnlyList<InteractiveBrowserSessionInfo>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            return _sessions.Values
                .Select(entry => entry.Info)
                .OrderBy(entry => entry.ProfileName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<InteractiveBrowserSessionInfo> StartAsync(
        string profileName,
        string? url = null,
        CancellationToken cancellationToken = default)
    {
        BrowserProfileNameRules.Validate(profileName);
        var maxDuration = GetMaxDuration();

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (_sessions.Values.Any(entry =>
                    string.Equals(
                        entry.Info.ProfileName,
                        profileName,
                        StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"Browser profile '{profileName}' already has an active interactive session.");
            }

            var session = await profileService.OpenInteractiveAsync(
                profileName,
                url,
                cancellationToken);

            var startedAt = timeProvider.GetUtcNow();
            var info = new InteractiveBrowserSessionInfo
            {
                SessionId = Guid.NewGuid(),
                ProfileName = profileName,
                InitialUrl = string.IsNullOrWhiteSpace(url) ? null : url,
                StartedAt = startedAt,
                ExpiresAt = startedAt.Add(maxDuration),
            };
            var expirationCancellation = new CancellationTokenSource();

            _sessions.Add(
                info.SessionId,
                new ActiveSession(
                    info,
                    session,
                    expirationCancellation));

            _ = ObserveCompletionAsync(
                info.SessionId,
                session);
            _ = ExpireAsync(
                info.SessionId,
                session,
                maxDuration,
                expirationCancellation.Token);

            return info;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> StopAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var activeSession = await RemoveActiveSessionAsync(
            sessionId,
            expectedSession: null,
            cancellationToken);

        if (activeSession is null)
        {
            return false;
        }

        CancelExpiration(activeSession);
        await activeSession.Session.DisposeAsync();
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        ActiveSession[] sessions;

        await _gate.WaitAsync();

        try
        {
            sessions = _sessions.Values.ToArray();
            _sessions.Clear();
        }
        finally
        {
            _gate.Release();
        }

        foreach (var activeSession in sessions)
        {
            CancelExpiration(activeSession);
            await TryDisposeAsync(activeSession.Session);
        }
    }

    private TimeSpan GetMaxDuration()
    {
        var maxDurationSeconds =
            options.InteractiveBrowser.MaxDurationSeconds;

        if (maxDurationSeconds < 1)
        {
            throw new InvalidOperationException(
                "bot.interactiveBrowser.maxDurationSeconds must be at least 1.");
        }

        return TimeSpan.FromSeconds(maxDurationSeconds);
    }

    private async Task ObserveCompletionAsync(
        Guid sessionId,
        IInteractiveBrowserSession session)
    {
        try
        {
            await session.Completion;
        }
        catch
        {
            // Completion is a lifecycle signal. Runtime failures are surfaced
            // by the browser/session layer and cleanup still has to run.
        }

        var activeSession = await RemoveActiveSessionAsync(
            sessionId,
            session,
            CancellationToken.None);

        if (activeSession is null)
        {
            return;
        }

        CancelExpiration(activeSession);
        await TryDisposeAsync(activeSession.Session);
    }

    private async Task ExpireAsync(
        Guid sessionId,
        IInteractiveBrowserSession session,
        TimeSpan maxDuration,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(
                maxDuration,
                timeProvider,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var activeSession = await RemoveActiveSessionAsync(
            sessionId,
            session,
            CancellationToken.None);

        if (activeSession is null)
        {
            return;
        }

        CancelExpiration(activeSession);
        await TryDisposeAsync(activeSession.Session);
    }

    private async Task<ActiveSession?> RemoveActiveSessionAsync(
        Guid sessionId,
        IInteractiveBrowserSession? expectedSession,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (!_sessions.TryGetValue(
                    sessionId,
                    out var activeSession) ||
                (expectedSession is not null &&
                 !ReferenceEquals(
                     activeSession.Session,
                     expectedSession)))
            {
                return null;
            }

            _sessions.Remove(sessionId);
            return activeSession;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static void CancelExpiration(
        ActiveSession activeSession)
    {
        activeSession.ExpirationCancellation.Cancel();
        activeSession.ExpirationCancellation.Dispose();
    }

    private static async Task TryDisposeAsync(
        IInteractiveBrowserSession session)
    {
        try
        {
            await session.DisposeAsync();
        }
        catch
        {
            // The browser may already be closed/crashed. Cleanup is best effort
            // after the session has been removed from the active registry.
        }
    }

    private sealed record ActiveSession(
        InteractiveBrowserSessionInfo Info,
        IInteractiveBrowserSession Session,
        CancellationTokenSource ExpirationCancellation);
}
