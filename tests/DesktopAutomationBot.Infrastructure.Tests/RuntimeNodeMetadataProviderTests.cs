using DesktopAutomationBot.Application;
using DesktopAutomationBot.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DesktopAutomationBot.Infrastructure.Tests;

public sealed class RuntimeNodeMetadataProviderTests
{
    [Fact]
    public async Task GetAsync_CombinesConfiguredAndRuntimeMetadata()
    {
        var provider = new RuntimeNodeMetadataProvider(
            new BotOptions
            {
                Node = new NodeOptions
                {
                    DisplayName = "  lab-worker  ",
                    Tags =
                    [
                        " region:home-lab ",
                        "REGION:HOME-LAB",
                    ],
                    Capabilities =
                    [
                        " gpu ",
                    ],
                    ExecutionSlots = 3,
                },
            },
            NullLogger<RuntimeNodeMetadataProvider>.Instance);

        var metadata = await provider.GetAsync();

        metadata.DisplayName.Should().Be("lab-worker");
        metadata.OperatingSystem.Should().NotBeNullOrWhiteSpace();
        metadata.DABotVersion.Should().NotBeNullOrWhiteSpace();
        metadata.Tags.Should()
            .Equal("region:home-lab");
        metadata.Capabilities.Should()
            .Contain("gpu")
            .And.Contain("interactive");
        metadata.ExecutionSlots.Should().Be(3);
    }

    [Fact]
    public async Task GetAsync_InvalidExecutionSlots_Throws()
    {
        var provider = new RuntimeNodeMetadataProvider(
            new BotOptions
            {
                Node = new NodeOptions
                {
                    ExecutionSlots = 0,
                },
            },
            NullLogger<RuntimeNodeMetadataProvider>.Instance);

        Func<Task> action = async () =>
            await provider.GetAsync();

        await action.Should()
            .ThrowAsync<ArgumentOutOfRangeException>()
            .WithMessage("*Execution slots must be greater than zero*");
    }
}
