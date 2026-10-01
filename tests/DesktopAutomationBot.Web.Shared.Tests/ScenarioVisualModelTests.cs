using System.Text.Json.Nodes;
using DesktopAutomationBot.Web.Shared;
using FluentAssertions;

namespace DesktopAutomationBot.Web.Shared.Tests;

public sealed class ScenarioVisualModelTests
{
    [Fact]
    public void MoveStep_ReordersModelAndJsonAndPreservesExtensionFields()
    {
        const string json = """
            {
              "$schema": "../schemas/scenario.schema.json",
              "name": "Reorder",
              "customMetadata": {
                "owner": "test"
              },
              "steps": [
                {
                  "id": "one",
                  "type": "OpenUrl",
                  "url": "https://example.com/one"
                },
                {
                  "id": "two",
                  "type": "Screenshot"
                },
                {
                  "id": "three",
                  "type": "Screenshot"
                }
              ]
            }
            """;

        ScenarioVisualModel.TryCreate(
                json,
                out var model,
                out var errors)
            .Should()
            .BeTrue(string.Join(Environment.NewLine, errors));

        var second = model!.Steps[1];

        model.MoveStep(second, -1).Should().BeTrue();
        model.Steps.Select(step => step.Id)
            .Should()
            .Equal("two", "one", "three");

        var root = JsonNode.Parse(model.ToJson())!.AsObject();
        root["$schema"]!.GetValue<string>()
            .Should()
            .Be("../schemas/scenario.schema.json");
        root["customMetadata"]!["owner"]!.GetValue<string>()
            .Should()
            .Be("test");
        root["steps"]!.AsArray()
            .Select(step => step!["id"]!.GetValue<string>())
            .Should()
            .Equal("two", "one", "three");
    }

    [Fact]
    public void MoveChild_ReordersNestedModelAndJson()
    {
        const string json = """
            {
              "name": "Nested reorder",
              "steps": [
                {
                  "id": "loop",
                  "type": "Loop",
                  "value": "2",
                  "children": [
                    {
                      "id": "first-child",
                      "type": "Screenshot"
                    },
                    {
                      "id": "second-child",
                      "type": "Screenshot"
                    }
                  ]
                }
              ]
            }
            """;

        ScenarioVisualModel.TryCreate(
                json,
                out var model,
                out var errors)
            .Should()
            .BeTrue(string.Join(Environment.NewLine, errors));

        var parent = model!.Steps.Single();
        var secondChild = parent.Children[1];

        parent.MoveChild(secondChild, -1).Should().BeTrue();
        parent.Children.Select(step => step.Id)
            .Should()
            .Equal("second-child", "first-child");

        var root = JsonNode.Parse(model.ToJson())!.AsObject();
        root["steps"]![0]!["children"]!
            .AsArray()
            .Select(step => step!["id"]!.GetValue<string>())
            .Should()
            .Equal("second-child", "first-child");
    }

    [Fact]
    public void MoveStep_AtBoundary_DoesNotChangeOrder()
    {
        const string json = """
            {
              "name": "Boundary reorder",
              "steps": [
                {
                  "id": "first",
                  "type": "OpenUrl",
                  "url": "https://example.com"
                },
                {
                  "id": "second",
                  "type": "Screenshot"
                }
              ]
            }
            """;

        ScenarioVisualModel.TryCreate(
                json,
                out var model,
                out var errors)
            .Should()
            .BeTrue(string.Join(Environment.NewLine, errors));

        model!.MoveStep(model.Steps[0], -1).Should().BeFalse();
        model.MoveStep(model.Steps[^1], 1).Should().BeFalse();
        model.Steps.Select(step => step.Id)
            .Should()
            .Equal("first", "second");
    }
    [Fact]
    public void Enabled_DefaultsToTrueAndFalseRoundTripsToJson()
    {
        const string json = """
            {
              "name": "Toggle",
              "steps": [
                {
                  "id": "capture",
                  "type": "Screenshot"
                }
              ]
            }
            """;

        ScenarioVisualModel.TryCreate(
                json,
                out var model,
                out var errors)
            .Should()
            .BeTrue(string.Join(Environment.NewLine, errors));

        var step = model!.Steps.Single();
        step.Enabled.Should().BeTrue();

        step.Enabled = false;

        var disabled = JsonNode.Parse(model.ToJson())!.AsObject();
        disabled["steps"]![0]!["enabled"]!
            .GetValue<bool>()
            .Should()
            .BeFalse();

        step.Enabled = true;

        var enabled = JsonNode.Parse(model.ToJson())!.AsObject();
        enabled["steps"]![0]!.AsObject()
            .ContainsKey("enabled")
            .Should()
            .BeFalse();
    }

}
