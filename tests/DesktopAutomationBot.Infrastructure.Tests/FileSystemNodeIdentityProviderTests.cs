using DesktopAutomationBot.Application;
using DesktopAutomationBot.Infrastructure;
using FluentAssertions;

namespace DesktopAutomationBot.Infrastructure.Tests;

public sealed class FileSystemNodeIdentityProviderTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(
            Path.GetTempPath(),
            "dabot-node-identity-tests",
            Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task GetAsync_FirstUse_CreatesPersistentNonEmptyNodeId()
    {
        var provider = CreateProvider();

        var identity = await provider.GetAsync();

        identity.NodeId.Should().NotBe(Guid.Empty);

        var path = Path.Combine(
            _directory,
            "node-id");

        File.Exists(path).Should().BeTrue();
        (await File.ReadAllTextAsync(path))
            .Trim()
            .Should()
            .Be(identity.NodeId.ToString("D"));
    }

    [Fact]
    public async Task GetAsync_NewProviderForSameStateDirectory_ReusesNodeId()
    {
        var first = await CreateProvider().GetAsync();
        var second = await CreateProvider().GetAsync();

        second.NodeId.Should().Be(first.NodeId);
    }

    [Fact]
    public async Task GetAsync_ConcurrentFirstUse_ConvergesOnSinglePersistedNodeId()
    {
        var firstProvider = CreateProvider();
        var secondProvider = CreateProvider();

        var identities = await Task.WhenAll(
            firstProvider.GetAsync(),
            secondProvider.GetAsync());

        identities[0].NodeId.Should()
            .Be(identities[1].NodeId);
    }

    [Fact]
    public async Task GetAsync_CorruptPersistedIdentity_FailsWithoutReplacingIt()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(
            _directory,
            "node-id");
        await File.WriteAllTextAsync(
            path,
            "not-a-guid");

        var provider = CreateProvider();

        Func<Task> action = async () =>
            await provider.GetAsync();

        await action.Should()
            .ThrowAsync<InvalidDataException>()
            .WithMessage("*does not contain a valid non-empty GUID*");

        (await File.ReadAllTextAsync(path))
            .Should()
            .Be("not-a-guid");
    }

    private FileSystemNodeIdentityProvider CreateProvider() =>
        new(
            new BotOptions
            {
                Node = new NodeOptions
                {
                    IdentityPath = Path.Combine(
                        _directory,
                        "node-id"),
                },
            });

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(
                _directory,
                recursive: true);
        }
    }
}
