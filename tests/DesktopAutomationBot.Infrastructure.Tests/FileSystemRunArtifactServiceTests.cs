using DesktopAutomationBot.Application;
using DesktopAutomationBot.Infrastructure;

namespace DesktopAutomationBot.Infrastructure.Tests;

public sealed class FileSystemRunArtifactServiceTests : IDisposable
{
    private readonly string _root =
        Path.Combine(
            Path.GetTempPath(),
            $"dabot-artifacts-{Guid.NewGuid():N}");

    [Fact]
    public async Task ListAsync_ReturnsScreenshotsAndArtifacts()
    {
        var runId = Guid.NewGuid();
        var options = CreateOptions();
        var screenshotDirectory = Path.Combine(
            options.Storage.ScreenshotsDirectory,
            runId.ToString("D"));
        var artifactDirectory = Path.Combine(
            options.Storage.ArtifactsDirectory,
            runId.ToString("D"));

        Directory.CreateDirectory(screenshotDirectory);
        Directory.CreateDirectory(artifactDirectory);

        await File.WriteAllBytesAsync(
            Path.Combine(screenshotDirectory, "step.png"),
            [1, 2, 3]);
        await File.WriteAllTextAsync(
            Path.Combine(artifactDirectory, "run-report.json"),
            "{}");
        await File.WriteAllTextAsync(
            Path.Combine(artifactDirectory, "failure.html"),
            "<html></html>");

        var service = new FileSystemRunArtifactService(options);

        var items = await service.ListAsync(runId);

        Assert.Equal(3, items.Count);
        Assert.Contains(
            items,
            item =>
                item.FileName == "step.png" &&
                item.Source == RunArtifactSource.Screenshots &&
                item.CanPreview);
        Assert.Contains(
            items,
            item =>
                item.FileName == "run-report.json" &&
                item.Source == RunArtifactSource.Artifacts &&
                item.CanPreview);
        Assert.Contains(
            items,
            item =>
                item.FileName == "failure.html" &&
                item.Source == RunArtifactSource.Artifacts &&
                !item.CanPreview);
    }

    [Fact]
    public async Task OpenAsync_OpensDirectChildWithSafeContentMetadata()
    {
        var runId = Guid.NewGuid();
        var options = CreateOptions();
        var artifactDirectory = Path.Combine(
            options.Storage.ArtifactsDirectory,
            runId.ToString("D"));
        Directory.CreateDirectory(artifactDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(artifactDirectory, "failure.html"),
            "<html>snapshot</html>");

        var service = new FileSystemRunArtifactService(options);

        var content = await service.OpenAsync(
            runId,
            RunArtifactSource.Artifacts,
            "failure.html");

        Assert.NotNull(content);
        Assert.False(content.Inline);
        Assert.Equal("text/html; charset=utf-8", content.ContentType);

        await using var stream = content.Content;
        using var reader = new StreamReader(stream);
        Assert.Equal("<html>snapshot</html>", await reader.ReadToEndAsync());
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("nested/file.txt")]
    [InlineData("nested\\file.txt")]
    public async Task OpenAsync_RejectsPathTraversal(string fileName)
    {
        var service = new FileSystemRunArtifactService(CreateOptions());

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.OpenAsync(
                Guid.NewGuid(),
                RunArtifactSource.Artifacts,
                fileName));
    }

    [Fact]
    public async Task OpenAsync_RejectsSymbolicLinkOutsideRunDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var runId = Guid.NewGuid();
        var options = CreateOptions();
        var artifactDirectory = Path.Combine(
            options.Storage.ArtifactsDirectory,
            runId.ToString("D"));
        Directory.CreateDirectory(artifactDirectory);

        var outsidePath = Path.Combine(_root, "outside-secret.txt");
        await File.WriteAllTextAsync(outsidePath, "secret");

        var linkPath = Path.Combine(artifactDirectory, "linked.txt");
        File.CreateSymbolicLink(linkPath, outsidePath);

        var service = new FileSystemRunArtifactService(options);

        var items = await service.ListAsync(runId);
        var content = await service.OpenAsync(
            runId,
            RunArtifactSource.Artifacts,
            "linked.txt");

        Assert.DoesNotContain(items, item => item.FileName == "linked.txt");
        Assert.Null(content);
    }

    [Fact]
    public async Task ListAsync_ReturnsEmptyWhenRunDirectoriesDoNotExist()
    {
        var service = new FileSystemRunArtifactService(CreateOptions());

        var items = await service.ListAsync(Guid.NewGuid());

        Assert.Empty(items);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private BotOptions CreateOptions() =>
        new()
        {
            Storage = new StorageOptions
            {
                ScreenshotsDirectory = Path.Combine(_root, "screenshots"),
                ArtifactsDirectory = Path.Combine(_root, "artifacts"),
                ScenariosDirectory = Path.Combine(_root, "scenarios"),
                BrowserProfilesDirectory = Path.Combine(_root, "profiles"),
                DatabasePath = Path.Combine(_root, "dabot.db"),
            },
        };
}
