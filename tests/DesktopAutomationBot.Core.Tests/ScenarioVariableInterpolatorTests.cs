using System.Text.Json;
using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Core.Tests;

public sealed class ScenarioVariableInterpolatorTests
{
    [Fact]
    public void Resolve_InterpolatesStringFieldsParametersAndChildren()
    {
        var variables = new ScenarioVariableBag();
        variables.Set("host", "example.com");
        variables.Set("text", "hello");
        variables.Set("selector", "#target");

        var step = new ScenarioStep
        {
            Type = StepType.FillText,
            Selector = "{{selector}}",
            Value = "{{text}} from {{host}}",
            Url = "https://{{host}}/path",
            Parameters = new Dictionary<string, JsonElement>
            {
                ["label"] = JsonSerializer.SerializeToElement("{{text}}"),
                ["count"] = JsonSerializer.SerializeToElement(2),
            },
            Children =
            [
                new ScenarioStep
                {
                    Type = StepType.OpenUrl,
                    Url = "https://{{host}}/child",
                },
            ],
        };

        var resolved = ScenarioVariableInterpolator.Resolve(step, variables);

        resolved.Selector.Should().Be("#target");
        resolved.Value.Should().Be("hello from example.com");
        resolved.Url.Should().Be("https://example.com/path");
        resolved.Parameters!["label"].GetString().Should().Be("hello");
        resolved.Parameters["count"].GetInt32().Should().Be(2);
        resolved.Children[0].Url.Should().Be("https://example.com/child");
    }


    [Fact]
    public void Resolve_ExactParameterReference_PreservesStructuredJsonType()
    {
        var variables = new ScenarioVariableBag();
        variables.Set(
            "count",
            ScenarioVariableValue.FromNumber(3));
        variables.Set(
            "payload",
            ScenarioVariableValue.ParseJson("""{"enabled":true,"ids":[1,2]}"""));

        var step = new ScenarioStep
        {
            Type = StepType.CallApi,
            Value = "count={{count}}",
            Parameters = new Dictionary<string, JsonElement>
            {
                ["count"] = JsonSerializer.SerializeToElement("{{count}}"),
                ["payload"] = JsonSerializer.SerializeToElement("{{payload}}"),
            },
        };

        var resolved = ScenarioVariableInterpolator.Resolve(
            step,
            variables);

        resolved.Value.Should().Be("count=3");
        resolved.Parameters!["count"].ValueKind.Should().Be(JsonValueKind.Number);
        resolved.Parameters["count"].GetInt32().Should().Be(3);
        resolved.Parameters["payload"].ValueKind.Should().Be(JsonValueKind.Object);
        resolved.Parameters["payload"].GetProperty("enabled").GetBoolean().Should().BeTrue();
        resolved.Parameters["payload"].GetProperty("ids").GetArrayLength().Should().Be(2);
    }


    [Fact]
    public void Resolve_InterpolatesRichLocatorValue()
    {
        var variables = new ScenarioVariableBag();
        variables.Set("button", "Save");

        var step = new ScenarioStep
        {
            Type = StepType.Click,
            Locator = new ScenarioLocator
            {
                Kind = ScenarioLocatorKind.Text,
                Value = "{{button}}",
                Exact = true,
            },
        };

        var resolved = ScenarioVariableInterpolator.Resolve(
            step,
            variables);

        resolved.Locator.Should().NotBeNull();
        resolved.Locator!.Kind.Should().Be(ScenarioLocatorKind.Text);
        resolved.Locator.Value.Should().Be("Save");
        resolved.Locator.Exact.Should().BeTrue();
    }

    [Fact]
    public void ResolveText_WhenVariableIsMissing_ThrowsHelpfulError()
    {
        var variables = new ScenarioVariableBag();

        Action action = () =>
            ScenarioVariableInterpolator.ResolveText(
                "https://{{missing}}",
                variables);

        action.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Scenario variable 'missing' is not defined.");
    }

    [Theory]
    [InlineData("{{name}}", true)]
    [InlineData("{{ name }}", true)]
    [InlineData("prefix-{{name}}", false)]
    [InlineData("{{}}", false)]
    [InlineData("plain", false)]
    public void IsExactVariableReference_RecognizesWholeValueReferences(
        string value,
        bool expected)
    {
        ScenarioVariableInterpolator
            .IsExactVariableReference(value)
            .Should()
            .Be(expected);
    }
}
