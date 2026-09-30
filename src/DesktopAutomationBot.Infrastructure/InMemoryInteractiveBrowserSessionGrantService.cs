using System.Security.Cryptography;
using System.Text;
using DesktopAutomationBot.Application;

namespace DesktopAutomationBot.Infrastructure;

public sealed class InMemoryInteractiveBrowserSessionGrantService(
    IInteractiveBrowserSessionService sessions,
    BotOptions options,
    TimeProvider timeProvider) :
    IInteractiveBrowserSessionGrantService
{
    private const int TokenBytes = 32;
    private const int MaxGrantLifetimeSeconds = 300;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<StoredGrant> _grants = [];

    public async Task<InteractiveBrowserSessionGrant> IssueAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var lifetime = GetGrantLifetime();
        await EnsureSessionIsActiveAsync(
            sessionId,
            cancellationToken);

        var now = timeProvider.GetUtcNow();
        var token = CreateToken();
        var tokenHash = HashToken(token);
        var expiresAt = now.Add(lifetime);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            RemoveExpiredUnsafe(now);
            _grants.RemoveAll(grant =>
                grant.SessionId == sessionId);

            _grants.Add(
                new StoredGrant(
                    sessionId,
                    tokenHash,
                    expiresAt));
        }
        finally
        {
            _gate.Release();
        }

        return new InteractiveBrowserSessionGrant
        {
            SessionId = sessionId,
            AccessToken = token,
            ExpiresAt = expiresAt,
        };
    }

    public async Task<bool> ConsumeAsync(
        Guid sessionId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return false;
        }

        if (!await IsSessionActiveAsync(
                sessionId,
                cancellationToken))
        {
            return false;
        }

        var candidateHash = HashToken(accessToken);
        var now = timeProvider.GetUtcNow();

        await _gate.WaitAsync(cancellationToken);

        try
        {
            RemoveExpiredUnsafe(now);

            for (var index = 0; index < _grants.Count; index++)
            {
                var grant = _grants[index];

                if (grant.SessionId != sessionId ||
                    !CryptographicOperations.FixedTimeEquals(
                        grant.TokenHash,
                        candidateHash))
                {
                    continue;
                }

                _grants.RemoveAt(index);
                return true;
            }

            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    private TimeSpan GetGrantLifetime()
    {
        var lifetimeSeconds =
            options.InteractiveBrowser.GrantLifetimeSeconds;

        if (lifetimeSeconds is < 1 or > MaxGrantLifetimeSeconds)
        {
            throw new InvalidOperationException(
                $"bot.interactiveBrowser.grantLifetimeSeconds must be between 1 and {MaxGrantLifetimeSeconds}.");
        }

        return TimeSpan.FromSeconds(lifetimeSeconds);
    }

    private async Task EnsureSessionIsActiveAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        if (!await IsSessionActiveAsync(
                sessionId,
                cancellationToken))
        {
            throw new InvalidOperationException(
                $"Interactive browser session '{sessionId}' is not active.");
        }
    }

    private async Task<bool> IsSessionActiveAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var activeSessions = await sessions.ListAsync(
            cancellationToken);

        return activeSessions.Any(session =>
            session.SessionId == sessionId);
    }

    private void RemoveExpiredUnsafe(DateTimeOffset now) =>
        _grants.RemoveAll(grant =>
            grant.ExpiresAt <= now);

    private static string CreateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenBytes);

        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] HashToken(string token) =>
        SHA256.HashData(
            Encoding.UTF8.GetBytes(token));

    private sealed record StoredGrant(
        Guid SessionId,
        byte[] TokenHash,
        DateTimeOffset ExpiresAt);
}
