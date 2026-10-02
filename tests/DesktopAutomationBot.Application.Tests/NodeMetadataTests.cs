using DesktopAutomationBot.Application;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class NodeMetadataTests
{
    [Fact]
    public void Validate_NormalizesConfiguredMetadata()
    {
        var metadata = NodeMetadata.Validate(
            new NodeMetadata
            {
                DisplayName = "  worker-1  ",
                OperatingSystem = " Linux ",
                DABotVersion = " 0.1.0 ",
                BrowserVersions =
                    new Dictionary<string, string>
                    {
                        [" chromium "] = " 140.0 ",
                        ["empty"] = " ",
                    },
                Tags =
                [
                    " home-lab ",
                    "HOME-LAB",
                    " ",
                ],
                Capabilities =
                [
                    " linux ",
                    "chromium",
                    "LINUX",
                ],
                ExecutionSlots = 2,
            });

        metadata.DisplayName.Should().Be("worker-1");
        metadata.OperatingSystem.Should().Be("Linux");
        metadata.DABotVersion.Should().Be("0.1.0");
        metadata.BrowserVersions.Should()
            .ContainSingle("chromium", "140.0");
        metadata.Tags.Should()
            .Equal("home-lab");
        metadata.Capabilities.Should()
            .Equal("chromium", "linux");
        metadata.ExecutionSlots.Should().Be(2);
    }

    [Fact]
    public void Validate_NonPositiveExecutionSlots_Throws()
    {
        var metadata = new NodeMetadata
        {
            DisplayName = "worker-1",
            OperatingSystem = "Linux",
            DABotVersion = "0.1.0",
            ExecutionSlots = 0,
        };

        var action = () =>
            NodeMetadata.Validate(metadata);

        action.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Execution slots must be greater than zero*");
    }
}
