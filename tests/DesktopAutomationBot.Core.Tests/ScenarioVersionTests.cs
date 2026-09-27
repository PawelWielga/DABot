using System.Text.Json;
using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Core.Tests;

public sealed class ScenarioVersionTests
{
    [Fact]
    public void Capture_CreatesImmutableSnapshotOfNormalizedDefinition()
    {
        var scenarioId = Guid.NewGuid();
        var createdAt = DateTimeOffset.Parse("2026-09-27T11:30:00+02:00");
        var scenario = CreateScenario();

        var version = ScenarioVersion.Capture(
            scenarioId,
            versionNumber: 1,
            scenario,
            createdAt);

        scenario.Name = "Changed";
        scenario.Steps[0] = scenario.Steps[0] with
        {
            Url = "https://changed.example.com",
        };

        var materialized = version.MaterializeDefinition();

        version.ScenarioId.Should().Be(scenarioId);
        version.VersionId.Should().NotBe(Guid.Empty);
        version.VersionNumber.Should().Be(1);
        version.SchemaVersion.Should().Be(ScenarioSchema.CurrentVersion);
        version.CreatedAt.Should().Be(createdAt);
        version.DefinitionHash.Should().MatchRegex("^[0-9a-f]{64}$");

        materialized.Name.Should().Be("Scenario");
        materialized.Steps[0].Id.Should().Be("step-001");
        materialized.Steps[0].Url.Should().Be("https://example.com");
    }

    [Fact]
    public void Capture_WhenLegacyAndExplicitIdsNormalizeToSameDefinition_ProducesSameHash()
    {
        var legacy = CreateScenario();
        var explicitIds = CreateScenario();
        explicitIds.Steps[0] = explicitIds.Steps[0] with { Id = "step-001" };

        var legacyVersion = ScenarioVersion.Capture(
            Guid.NewGuid(),
            1,
            legacy,
            DateTimeOffset.UtcNow);

        var explicitVersion = ScenarioVersion.Capture(
            Guid.NewGuid(),
            1,
            explicitIds,
            DateTimeOffset.UtcNow);

        legacyVersion.DefinitionHash.Should().Be(explicitVersion.DefinitionHash);
        legacyVersion.DefinitionJson.Should().Be(explicitVersion.DefinitionJson);
    }

    [Fact]
    public void Capture_WhenParameterDictionaryOrderDiffers_ProducesSameHash()
    {
        var first = CreateScenarioWithParameters(
            ("zeta", "last"),
            ("alpha", "first"));

        var second = CreateScenarioWithParameters(
            ("alpha", "first"),
            ("zeta", "last"));

        var firstVersion = ScenarioVersion.Capture(
            Guid.NewGuid(),
            1,
            first,
            DateTimeOffset.UtcNow);

        var secondVersion = ScenarioVersion.Capture(
            Guid.NewGuid(),
            1,
            second,
            DateTimeOffset.UtcNow);

        firstVersion.DefinitionHash.Should().Be(secondVersion.DefinitionHash);
        firstVersion.DefinitionJson.Should().Be(secondVersion.DefinitionJson);
    }

    [Fact]
    public void Capture_WhenDefinitionChanges_ProducesDifferentHash()
    {
        var first = CreateScenario();
        var second = CreateScenario();
        second.Steps[0] = second.Steps[0] with
        {
            Url = "https://other.example.com",
        };

        var firstVersion = ScenarioVersion.Capture(
            Guid.NewGuid(),
            1,
            first,
            DateTimeOffset.UtcNow);

        var secondVersion = ScenarioVersion.Capture(
            Guid.NewGuid(),
            1,
            second,
            DateTimeOffset.UtcNow);

        firstVersion.DefinitionHash.Should().NotBe(secondVersion.DefinitionHash);
    }

    [Fact]
    public void MaterializeDefinition_ReturnsIndependentCopies()
    {
        var version = ScenarioVersion.Capture(
            Guid.NewGuid(),
            1,
            CreateScenario(),
            DateTimeOffset.UtcNow);

        var first = version.MaterializeDefinition();
        var second = version.MaterializeDefinition();

        first.Steps[0] = first.Steps[0] with
        {
            Url = "https://mutated.example.com",
        };

        second.Steps[0].Url.Should().Be("https://example.com");
        version.MaterializeDefinition().Steps[0].Url.Should().Be("https://example.com");
    }

    [Fact]
    public void Capture_WhenScenarioIdIsEmpty_Throws()
    {
        var act = () => ScenarioVersion.Capture(
            Guid.Empty,
            1,
            CreateScenario(),
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Capture_WhenVersionNumberIsInvalid_Throws()
    {
        var act = () => ScenarioVersion.Capture(
            Guid.NewGuid(),
            0,
            CreateScenario(),
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static ScenarioDefinition CreateScenario() =>
        new()
        {
            Name = "Scenario",
            Steps =
            [
                new ScenarioStep
                {
                    Type = StepType.OpenUrl,
                    Url = "https://example.com",
                },
            ],
        };

    private static ScenarioDefinition CreateScenarioWithParameters(
        params (string Name, string Value)[] values)
    {
        var parameters = new Dictionary<string, JsonElement>();

        foreach (var (name, value) in values)
        {
            parameters.Add(
                name,
                JsonDocument.Parse(JsonSerializer.Serialize(value)).RootElement.Clone());
        }

        return new ScenarioDefinition
        {
            Name = "Parameters",
            Steps =
            [
                new ScenarioStep
                {
                    Id = "api",
                    Type = StepType.CallApi,
                    Parameters = parameters,
                },
            ],
        };
    }
}
