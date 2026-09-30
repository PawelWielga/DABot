namespace DesktopAutomationBot.Application;

public enum RunArtifactSource
{
    Screenshots,
    Artifacts,
}

public sealed record RunArtifactItem(
    string FileName,
    RunArtifactSource Source,
    long SizeBytes,
    DateTimeOffset LastModifiedAt,
    bool CanPreview);

public sealed record RunArtifactContent(
    Stream Content,
    string ContentType,
    string FileName,
    bool Inline);

public interface IRunArtifactService
{
    Task<IReadOnlyList<RunArtifactItem>> ListAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    Task<RunArtifactContent?> OpenAsync(
        Guid runId,
        RunArtifactSource source,
        string fileName,
        CancellationToken cancellationToken = default);
}
