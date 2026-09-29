using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class ScenarioRunRequestTests
{
    [Fact]
    public void CreateRun_UsesSuppliedIdentityCursorAndVariables()
    {
        var version = CreateVersion();
        var runId = Guid.NewGuid();
        var cursor = ExecutionCursor.Start(version.MaterializeDefinition());
        var request = new ScenarioRunRequest
        {
            ScenarioVersion = version,
            RunId = runId,
            Cursor = cursor,
            Variables =
            {
                ["input"] = "value",
            },
            StructuredVariables =
            {
                ["count"] = ScenarioVariableValue.FromNumber(4),
            },
        };

        var createdAt = DateTimeOffset.Parse("2026-09-27T12:30:00+02:00");
        var run = request.CreateRun(createdAt);

        run.RunId.Should().Be(runId);
        run.ScenarioVersionId.Should().Be(version.VersionId);
        run.Cursor.Should().BeEquivalentTo(cursor);
        run.Variables["input"].ToInterpolationString().Should().Be("value");
        run.Variables["count"].ToJsonElement().GetInt32().Should().Be(4);
        run.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void CreateRun_WhenRunIdIsOmitted_GeneratesIdentity()
    {
        var request = new ScenarioRunRequest
        {
            ScenarioVersion = CreateVersion(),
        };

        var run = request.CreateRun(DateTimeOffset.UtcNow);

        run.RunId.Should().NotBe(Guid.Empty);
    }

    private static ScenarioVersion CreateVersion() =>
        ScenarioVersion.Capture(
            Guid.NewGuid(),
            1,
            new ScenarioDefinition
            {
                Name = "Scenario",
                Steps =
                [
                    new ScenarioStep
                    {
                        Id = "open",
                        Type = StepType.OpenUrl,
                        Url = "https://example.com",
                    },
                ],
            },
            DateTimeOffset.UtcNow);
}
