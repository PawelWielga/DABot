using DesktopAutomationBot.Application;

namespace DesktopAutomationBot.Infrastructure;

public sealed class FileSystemRunArtifactService(
    BotOptions options) : IRunArtifactService
{
    public Task<IReadOnlyList<RunArtifactItem>> ListAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        ValidateRunId(runId);
        cancellationToken.ThrowIfCancellationRequested();

        var items = new List<RunArtifactItem>();
        AddDirectoryItems(
            items,
            runId,
            RunArtifactSource.Screenshots,
            options.Storage.ScreenshotsDirectory);
        AddDirectoryItems(
            items,
            runId,
            RunArtifactSource.Artifacts,
            options.Storage.ArtifactsDirectory);

        IReadOnlyList<RunArtifactItem> result = items
            .OrderByDescending(item => item.LastModifiedAt)
            .ThenBy(item => item.Source)
            .ThenBy(item => item.FileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult(result);
    }

    public Task<RunArtifactContent?> OpenAsync(
        Guid runId,
        RunArtifactSource source,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ValidateRunId(runId);
        ValidateFileName(fileName);
        cancellationToken.ThrowIfCancellationRequested();

        var root = source switch
        {
            RunArtifactSource.Screenshots => options.Storage.ScreenshotsDirectory,
            RunArtifactSource.Artifacts => options.Storage.ArtifactsDirectory,
            _ => throw new ArgumentOutOfRangeException(
                nameof(source),
                source,
                "Unknown run artifact source."),
        };

        var runDirectory = GetRunDirectory(root, runId);
        var fullPath = Path.GetFullPath(Path.Combine(runDirectory, fileName));

        if (!IsDirectChild(runDirectory, fullPath) || !File.Exists(fullPath))
        {
            return Task.FromResult<RunArtifactContent?>(null);
        }

        var (contentType, canPreview) = GetContentMetadata(fullPath);
        Stream stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 64 * 1024,
            useAsync: true);

        return Task.FromResult<RunArtifactContent?>(
            new RunArtifactContent(
                stream,
                contentType,
                Path.GetFileName(fullPath),
                canPreview));
    }

    private static void AddDirectoryItems(
        ICollection<RunArtifactItem> items,
        Guid runId,
        RunArtifactSource source,
        string root)
    {
        var directory = GetRunDirectory(root, runId);
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(
                     directory,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            var info = new FileInfo(path);
            var (_, canPreview) = GetContentMetadata(path);
            items.Add(
                new RunArtifactItem(
                    info.Name,
                    source,
                    info.Length,
                    new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                    canPreview));
        }
    }

    private static string GetRunDirectory(
        string root,
        Guid runId) =>
        Path.GetFullPath(
            Path.Combine(
                root,
                runId.ToString("D")));

    private static bool IsDirectChild(
        string directory,
        string path)
    {
        var parent = Path.GetDirectoryName(path);
        return parent is not null &&
               string.Equals(
                   Path.GetFullPath(parent),
                   Path.GetFullPath(directory),
                   OperatingSystem.IsWindows()
                       ? StringComparison.OrdinalIgnoreCase
                       : StringComparison.Ordinal);
    }

    private static void ValidateRunId(Guid runId)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException(
                "Run ID must not be empty.",
                nameof(runId));
        }
    }

    private static void ValidateFileName(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (!string.Equals(
                Path.GetFileName(fileName),
                fileName,
                StringComparison.Ordinal) ||
            fileName is "." or "..")
        {
            throw new ArgumentException(
                "Artifact file name must be a direct file name without path segments.",
                nameof(fileName));
        }
    }

    private static (string ContentType, bool CanPreview) GetContentMetadata(
        string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => ("image/png", true),
            ".jpg" or ".jpeg" => ("image/jpeg", true),
            ".gif" => ("image/gif", true),
            ".webp" => ("image/webp", true),
            ".txt" => ("text/plain; charset=utf-8", true),
            ".json" => ("application/json; charset=utf-8", true),
            ".html" or ".htm" => ("text/html; charset=utf-8", false),
            _ => ("application/octet-stream", false),
        };
}
