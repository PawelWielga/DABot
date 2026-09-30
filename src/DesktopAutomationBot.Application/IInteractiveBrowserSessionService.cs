namespace DesktopAutomationBot.Application;

public sealed record InteractiveBrowserSessionInfo
{
    public required Guid SessionId { get; init; }

    public required string ProfileName { get; init; }

    public string? InitialUrl { get; init; }

    public required DateTimeOffset StartedAt { get; init; }
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
    TimeProvider timeProvider) :
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

            var info = new InteractiveBrowserSessionInfo
            {
                SessionId = Guid.NewGuid(),
                ProfileName = profileName,
                InitialUrl = string.IsNullOrWhiteSpace(url) ? null : url,
                StartedAt = timeProvider.GetUtcNow(),
            };

            _sessions.Add(
                info.SessionId,
                new ActiveSession(info, session));

            _ = ObserveCompletionAsync(
                info.SessionId,
                session);

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
        ActiveSession? activeSession;

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (!_sessions.Remove(sessionId, out activeSession))
            {
                return false;
            }
        }
        finally
        {
            _gate.Release();
        }

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
            try
            {
                await activeSession.Session.DisposeAsync();
            }
            catch
            {
                // Best-effort process shutdown cleanup; individual close failures
                // must not prevent other profile leases from being released.
            }
        }

        _gate.Dispose();
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

        ActiveSession? activeSession;

        await _gate.WaitAsync();

        try
        {
            if (!_sessions.TryGetValue(sessionId, out activeSession) ||
                !ReferenceEquals(activeSession.Session, session))
            {
                return;
            }

            _sessions.Remove(sessionId);
        }
        finally
        {
            _gate.Release();
        }

        try
        {
            await session.DisposeAsync();
        }
        catch
        {
            // The browser may already be closed/crashed. Dispose is only
            // responsible for releasing remaining resources such as the lease.
        }
    }

    private sealed record ActiveSession(
        InteractiveBrowserSessionInfo Info,
        IInteractiveBrowserSession Session);
}
