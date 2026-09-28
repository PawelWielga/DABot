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
    public void ResolveText_WhenVariableIsMissing_ThrowsHelpfulError()
    {
        var variables = new ScenarioVariableBag();

        var action = () =>
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
