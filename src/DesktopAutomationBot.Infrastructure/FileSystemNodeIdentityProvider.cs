using DesktopAutomationBot.Application;

namespace DesktopAutomationBot.Infrastructure;

public sealed class FileSystemNodeIdentityProvider(
    BotOptions options) : INodeIdentityProvider
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _identityPath =
        ResolveIdentityPath(options);

    private NodeIdentity? _cached;

    public async Task<NodeIdentity> GetAsync(
        CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (_cached is not null)
            {
                return _cached;
            }

            _cached = await LoadOrCreateAsync(
                cancellationToken);
            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<NodeIdentity> LoadOrCreateAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_identityPath))
        {
            await TryCreateAsync(cancellationToken);
        }

        var value = (
            await File.ReadAllTextAsync(
                _identityPath,
                cancellationToken))
            .Trim();

        if (!Guid.TryParseExact(
                value,
                "D",
                out var nodeId) ||
            nodeId == Guid.Empty)
        {
            throw new InvalidDataException(
                $"Node identity file '{_identityPath}' does not contain a valid non-empty GUID.");
        }

        return new NodeIdentity
        {
            NodeId = nodeId,
        };
    }

    private async Task TryCreateAsync(
        CancellationToken cancellationToken)
    {
        var directory =
            Path.GetDirectoryName(_identityPath)
            ?? Directory.GetCurrentDirectory();

        Directory.CreateDirectory(directory);

        var nodeId = Guid.NewGuid();
        var temporaryPath = Path.Combine(
            directory,
            $".node-id.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                nodeId.ToString("D") + Environment.NewLine,
                cancellationToken);

            try
            {
                File.Move(
                    temporaryPath,
                    _identityPath);
            }
            catch (IOException)
                when (File.Exists(_identityPath))
            {
                // Another process won the first-start race. Read and use the
                // already persisted identity instead of rotating the node ID.
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string ResolveIdentityPath(
        BotOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var databasePath =
            Path.GetFullPath(options.Storage.DatabasePath);
        var stateDirectory =
            Path.GetDirectoryName(databasePath)
            ?? Directory.GetCurrentDirectory();

        return Path.Combine(
            stateDirectory,
            "node-id");
    }
}
