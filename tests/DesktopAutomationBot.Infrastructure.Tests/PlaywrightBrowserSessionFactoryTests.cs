using DesktopAutomationBot.Application;
using DesktopAutomationBot.Infrastructure;
using FluentAssertions;

namespace DesktopAutomationBot.Infrastructure.Tests;

public sealed class PlaywrightBrowserSessionFactoryTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(
            Path.GetTempPath(),
            "dabot-profile-tests",
            Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CreateAsync_WithNamedProfile_CreatesRuntimeDirectoryAndHoldsExclusiveLease()
    {
        var factory = CreateFactory();

        var first = await factory.CreateAsync(
            new BrowserSessionRequest
            {
                ProfileName = "work-account",
            });

        Directory.Exists(
            Path.Combine(
                _tempDirectory,
                "profiles",
                "work-account"))
            .Should()
            .BeTrue();

        Func<Task> secondCreate = async () =>
        {
            await factory.CreateAsync(
                new BrowserSessionRequest
                {
                    ProfileName = "work-account",
                });
        };

        await secondCreate.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*already in use*");

        await first.DisposeAsync();

        var second = await factory.CreateAsync(
            new BrowserSessionRequest
            {
                ProfileName = "work-account",
            });

        await second.DisposeAsync();
    }

    [Fact]
    public async Task CreateAsync_WithDifferentProfiles_AllowsIndependentLeases()
    {
        var factory = CreateFactory();

        var first = await factory.CreateAsync(
            new BrowserSessionRequest
            {
                ProfileName = "first",
            });
        var second = await factory.CreateAsync(
            new BrowserSessionRequest
            {
                ProfileName = "second",
            });

        first.Should().NotBeSameAs(second);

        await first.DisposeAsync();
        await second.DisposeAsync();
    }

    private PlaywrightBrowserSessionFactory CreateFactory() =>
        new(
            new BotOptions
            {
                Storage = new StorageOptions
                {
                    BrowserProfilesDirectory = Path.Combine(
                        _tempDirectory,
                        "profiles"),
                },
            });

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(
                _tempDirectory,
                recursive: true);
        }
    }
}
