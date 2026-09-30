using DesktopAutomationBot.Application;

namespace DesktopAutomationBot.Web.Demo;

public sealed class DemoRunArtifactService : IRunArtifactService
{
    public Task<IReadOnlyList<RunArtifactItem>> ListAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var now = DateTimeOffset.UtcNow;
        IReadOnlyList<RunArtifactItem> items =
        [
            new(
                "20260930102530123-step-2.png",
                RunArtifactSource.Screenshots,
                184_320,
                now.AddMinutes(-5),
                CanPreview: true),
            new(
                "run-report.json",
                RunArtifactSource.Artifacts,
                3_218,
                now.AddMinutes(-4),
                CanPreview: true),
            new(
                "failure.html",
                RunArtifactSource.Artifacts,
                41_905,
                now.AddMinutes(-4),
                CanPreview: false),
        ];

        return Task.FromResult(items);
    }

    public Task<RunArtifactContent?> OpenAsync(
        Guid runId,
        RunArtifactSource source,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<RunArtifactContent?>(null);
    }
}
