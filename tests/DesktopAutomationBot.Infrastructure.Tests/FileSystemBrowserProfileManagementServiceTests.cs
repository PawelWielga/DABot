using DesktopAutomationBot.Application;
using DesktopAutomationBot.Infrastructure;
using FluentAssertions;

namespace DesktopAutomationBot.Infrastructure.Tests;

public sealed class FileSystemBrowserProfileManagementServiceTests : IDisposable
{
    private readonly string _profilesRoot =
        Path.Combine(
            Path.GetTempPath(),
            "dabot-profile-management-tests",
            Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CreateRenameDeleteAsync_ManagesProfileDirectoryWithoutLosingContents()
    {
        var service = CreateService();

        await service.CreateAsync("work-account");

        var source = Path.Combine(_profilesRoot, "work-account");
        Directory.Exists(source).Should().BeTrue();
        await File.WriteAllTextAsync(
            Path.Combine(source, "marker.txt"),
            "preserve-me");

        await service.RenameAsync(
            "work-account",
            "renamed-account");

        var renamed = Path.Combine(_profilesRoot, "renamed-account");
        Directory.Exists(source).Should().BeFalse();
        Directory.Exists(renamed).Should().BeTrue();
        (await File.ReadAllTextAsync(Path.Combine(renamed, "marker.txt")))
            .Should()
            .Be("preserve-me");

        await service.DeleteAsync("renamed-account");

        Directory.Exists(renamed).Should().BeFalse();
    }

    [Fact]
    public async Task ClearAsync_RemovesProfileContentsButKeepsProfileDirectory()
    {
        var service = CreateService();
        await service.CreateAsync("clear-profile");

        var profileDirectory = Path.Combine(_profilesRoot, "clear-profile");
        Directory.CreateDirectory(Path.Combine(profileDirectory, "nested"));
        await File.WriteAllTextAsync(
            Path.Combine(profileDirectory, "nested", "marker.txt"),
            "remove-me");

        await service.ClearAsync("clear-profile");

        Directory.Exists(profileDirectory).Should().BeTrue();
        Directory.EnumerateFileSystemEntries(profileDirectory)
            .Should()
            .BeEmpty();
    }

    [Fact]
    public async Task ClearAsync_WhenProfileIsLeased_RejectsClear()
    {
        var service = CreateService();
        await service.CreateAsync("busy-clear-profile");

        var factory = new PlaywrightBrowserSessionFactory(CreateOptions());
        await using var session = await factory.CreateAsync(
            new BrowserSessionRequest
            {
                ProfileName = "busy-clear-profile",
            });

        Func<Task> clear = () => service.ClearAsync("busy-clear-profile");

        await clear.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*already in use*");
    }

    [Fact]
    public async Task DeleteAsync_WhenProfileIsLeased_RejectsDeletion()
    {
        var service = CreateService();
        await service.CreateAsync("busy-profile");

        var factory = new PlaywrightBrowserSessionFactory(CreateOptions());
        await using var session = await factory.CreateAsync(
            new BrowserSessionRequest
            {
                ProfileName = "busy-profile",
            });

        Func<Task> delete = () => service.DeleteAsync("busy-profile");

        await delete.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*already in use*");

        Directory.Exists(
            Path.Combine(_profilesRoot, "busy-profile"))
            .Should()
            .BeTrue();
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData(".hidden")]
    [InlineData("bad/name")]
    [InlineData("")]
    public async Task CreateAsync_WithInvalidName_RejectsProfile(string profileName)
    {
        var service = CreateService();

        Func<Task> create = () => service.CreateAsync(profileName);

        await create.Should().ThrowAsync<ArgumentException>();
    }

    private FileSystemBrowserProfileManagementService CreateService() =>
        new(CreateOptions());

    private BotOptions CreateOptions() =>
        new()
        {
            Storage = new StorageOptions
            {
                BrowserProfilesDirectory = _profilesRoot,
            },
        };

    public void Dispose()
    {
        if (Directory.Exists(_profilesRoot))
        {
            Directory.Delete(_profilesRoot, recursive: true);
        }
    }
}
