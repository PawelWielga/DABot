using DesktopAutomationBot.Application;
using DesktopAutomationBot.Infrastructure;
using FluentAssertions;

namespace DesktopAutomationBot.Infrastructure.Tests;

public sealed class FileSystemBrowserProfileCatalogTests
{
    [Fact]
    public async Task ListAsync_ReturnsProfileDirectoriesAndIgnoresInternalLocksDirectory()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "dabot-profile-catalog-tests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.Combine(root, ".locks"));
            Directory.CreateDirectory(Path.Combine(root, "zeta"));
            Directory.CreateDirectory(Path.Combine(root, "Alpha"));

            var catalog = new FileSystemBrowserProfileCatalog(
                new BotOptions
                {
                    Storage = new StorageOptions
                    {
                        BrowserProfilesDirectory = root,
                    },
                });

            var profiles = await catalog.ListAsync();

            profiles
                .Select(profile => profile.Name)
                .Should()
                .Equal("Alpha", "zeta");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ListAsync_WhenProfilesDirectoryDoesNotExist_ReturnsEmptyList()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "dabot-profile-catalog-tests",
            Guid.NewGuid().ToString("N"));

        var catalog = new FileSystemBrowserProfileCatalog(
            new BotOptions
            {
                Storage = new StorageOptions
                {
                    BrowserProfilesDirectory = root,
                },
            });

        var profiles = await catalog.ListAsync();

        profiles.Should().BeEmpty();
    }
}
