using System.Text.Json;
using System.Text.Json.Nodes;
using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Web.Shared;

public sealed class ScenarioVisualModel
{
    private static readonly JsonSerializerOptions IndentedJson = new()
    {
        WriteIndented = true,
    };

    private readonly JsonObject _root;
    private readonly JsonArray _steps;

    private ScenarioVisualModel(JsonObject root)
    {
        _root = root;
        _steps = root["steps"] as JsonArray ?? new JsonArray();
        _root["steps"] = _steps;

        Steps = _steps
            .OfType<JsonObject>()
            .Select(node => new ScenarioVisualStepModel(node))
            .ToList();
    }

    public List<ScenarioVisualStepModel> Steps { get; }

    public string Name
    {
        get => GetString(_root, "name") ?? string.Empty;
        set => _root["name"] = value;
    }

    public string? BrowserProfile
    {
        get => GetString(_root, "browserProfile");
        set => SetOptionalString(_root, "browserProfile", value);
    }

    public int? TimeoutMs
    {
        get => GetInt32(_root, "timeoutMs");
        set => SetNullableInt32(_root, "timeoutMs", value);
    }

    public static bool TryCreate(
        string json,
        out ScenarioVisualModel? model,
        out IReadOnlyList<string> errors)
    {
        var validation = ScenarioJsonValidation.Validate(json);
        if (!validation.IsValid)
        {
            model = null;
            errors = validation.Errors;
            return false;
        }

        try
        {
            if (JsonNode.Parse(json) is not JsonObject root ||
                root["steps"] is not JsonArray steps ||
                steps.Any(node => node is not JsonObject))
            {
                model = null;
                errors = ["Scenario JSON must contain an object with an array of step objects."];
                return false;
            }

            model = new ScenarioVisualModel(root);
            errors = [];
            return true;
        }
        catch (JsonException exception)
        {
            model = null;
            errors = [$"Scenario JSON could not be loaded into the visual editor: {exception.Message}"];
            return false;
        }
    }

    public string ToJson() =>
        _root.ToJsonString(IndentedJson);

    public void AddStep()
    {
        var node = CreateStep(
            StepType.OpenUrl,
            includeStarterValue: true);
        _steps.Add(node);
        Steps.Add(new ScenarioVisualStepModel(node));
    }

    public void RemoveStep(ScenarioVisualStepModel step)
    {
        ArgumentNullException.ThrowIfNull(step);
        _steps.Remove(step.Node);
        Steps.Remove(step);
    }

    internal static JsonObject CreateStep(
        StepType type,
        bool includeStarterValue = false)
    {
        var node = new JsonObject
        {
            ["type"] = type.ToString(),
        };

        if (includeStarterValue && type == StepType.OpenUrl)
        {
            node["url"] = "https://example.com";
        }

        return node;
    }

    internal static string? GetString(
        JsonObject node,
        string propertyName) =>
        node[propertyName] is JsonValue value &&
        value.TryGetValue<string>(out var result)
            ? result
            : null;

    internal static void SetOptionalString(
        JsonObject node,
        string propertyName,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            node.Remove(propertyName);
            return;
        }

        node[propertyName] = value;
    }

    internal static int? GetInt32(
        JsonObject node,
        string propertyName) =>
        node[propertyName] is JsonValue value &&
        value.TryGetValue<int>(out var result)
            ? result
            : null;

    internal static void SetNullableInt32(
        JsonObject node,
        string propertyName,
        int? value)
    {
        if (value.HasValue)
        {
            node[propertyName] = value.Value;
        }
        else
        {
            node.Remove(propertyName);
        }
    }

    internal static TEnum? GetEnum<TEnum>(
        JsonObject node,
        string propertyName)
        where TEnum : struct, Enum
    {
        var raw = GetString(node, propertyName);
        return Enum.TryParse<TEnum>(
            raw,
            ignoreCase: true,
            out var parsed)
            ? parsed
            : null;
    }

    internal static void SetNullableEnum<TEnum>(
        JsonObject node,
        string propertyName,
        TEnum? value)
        where TEnum : struct, Enum
    {
        if (value.HasValue)
        {
            node[propertyName] = value.Value.ToString();
        }
        else
        {
            node.Remove(propertyName);
        }
    }
}

public sealed class ScenarioVisualStepModel
{
    private static readonly JsonSerializerOptions IndentedJson = new()
    {
        WriteIndented = true,
    };

    private readonly JsonArray _children;

    internal ScenarioVisualStepModel(JsonObject node)
    {
        Node = node;
        _children = node["children"] as JsonArray ?? new JsonArray();

        if (node["children"] is JsonArray)
        {
            Children = _children
                .OfType<JsonObject>()
                .Select(child => new ScenarioVisualStepModel(child))
                .ToList();
        }
        else
        {
            Children = [];
        }
    }

    internal JsonObject Node { get; }

    public List<ScenarioVisualStepModel> Children { get; }

    public string? Id
    {
        get => ScenarioVisualModel.GetString(Node, "id");
        set => ScenarioVisualModel.SetOptionalString(Node, "id", value);
    }

    public StepType Type
    {
        get => ScenarioVisualModel.GetEnum<StepType>(Node, "type") ?? StepType.OpenUrl;
        set => Node["type"] = value.ToString();
    }

    public string? Selector
    {
        get => ScenarioVisualModel.GetString(Node, "selector");
        set => ScenarioVisualModel.SetOptionalString(Node, "selector", value);
    }

    public string? Url
    {
        get => ScenarioVisualModel.GetString(Node, "url");
        set => ScenarioVisualModel.SetOptionalString(Node, "url", value);
    }

    public string? Value
    {
        get => ScenarioVisualModel.GetString(Node, "value");
        set => ScenarioVisualModel.SetOptionalString(Node, "value", value);
    }

    public string? Output
    {
        get => ScenarioVisualModel.GetString(Node, "output");
        set => ScenarioVisualModel.SetOptionalString(Node, "output", value);
    }

    public int? TimeoutMs
    {
        get => ScenarioVisualModel.GetInt32(Node, "timeoutMs");
        set => ScenarioVisualModel.SetNullableInt32(Node, "timeoutMs", value);
    }

    public int? RetryCount
    {
        get => ScenarioVisualModel.GetInt32(Node, "retryCount");
        set => ScenarioVisualModel.SetNullableInt32(Node, "retryCount", value);
    }

    public int? RetryDelayMs
    {
        get => ScenarioVisualModel.GetInt32(Node, "retryDelayMs");
        set => ScenarioVisualModel.SetNullableInt32(Node, "retryDelayMs", value);
    }

    public StepRetrySafety? RetrySafety
    {
        get => ScenarioVisualModel.GetEnum<StepRetrySafety>(Node, "retrySafety");
        set => ScenarioVisualModel.SetNullableEnum(Node, "retrySafety", value);
    }

    public bool UsesStructuredLocator =>
        Node["locator"] is JsonObject;

    public ScenarioLocatorKind LocatorKind
    {
        get => Node["locator"] is JsonObject locator
            ? ScenarioVisualModel.GetEnum<ScenarioLocatorKind>(locator, "kind")
                ?? ScenarioLocatorKind.Selector
            : ScenarioLocatorKind.Selector;
        set
        {
            var locator = EnsureLocator();
            locator["kind"] = value.ToString();
        }
    }

    public string? LocatorValue
    {
        get => Node["locator"] is JsonObject locator
            ? ScenarioVisualModel.GetString(locator, "value")
            : null;
        set
        {
            var locator = EnsureLocator();
            locator["value"] = value ?? string.Empty;
        }
    }

    public bool LocatorExact
    {
        get =>
            Node["locator"] is JsonObject locator &&
            locator["exact"] is JsonValue value &&
            value.TryGetValue<bool>(out var exact) &&
            exact;
        set
        {
            var locator = EnsureLocator();
            locator["exact"] = value;
        }
    }

    public bool SupportsLocator =>
        Type is StepType.Click or
            StepType.FillText or
            StepType.PasteText or
            StepType.ReadText or
            StepType.WaitFor;

    public bool SupportsUrl =>
        Type is StepType.OpenUrl or StepType.CallApi;

    public bool SupportsValue =>
        Type is StepType.FillText or
            StepType.PasteText or
            StepType.WaitFor or
            StepType.CallApi or
            StepType.Delay or
            StepType.If or
            StepType.Loop;

    public bool SupportsOutput =>
        Type is StepType.ReadText or StepType.CallApi;

    public bool SupportsParameters =>
        Type is StepType.WaitFor or
            StepType.CallApi or
            StepType.Suspend ||
        Node["parameters"] is not null;

    public bool SupportsChildren =>
        Type is StepType.If or StepType.Loop ||
        Children.Count > 0;

    public void UseStructuredLocator()
    {
        var selector = Selector;
        Node.Remove("selector");

        Node["locator"] = new JsonObject
        {
            ["kind"] = ScenarioLocatorKind.Selector.ToString(),
            ["value"] = selector ?? string.Empty,
            ["exact"] = false,
        };
    }

    public void UseSelector()
    {
        if (Node["locator"] is JsonObject locator &&
            ScenarioVisualModel.GetEnum<ScenarioLocatorKind>(locator, "kind") ==
                ScenarioLocatorKind.Selector)
        {
            Selector = ScenarioVisualModel.GetString(locator, "value");
        }

        Node.Remove("locator");
    }

    public string GetParametersJson() =>
        Node["parameters"] is JsonObject parameters
            ? parameters.ToJsonString(IndentedJson)
            : string.Empty;

    public bool TrySetParametersJson(
        string? json,
        out string? error)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            Node.Remove("parameters");
            error = null;
            return true;
        }

        try
        {
            if (JsonNode.Parse(json) is not JsonObject parameters)
            {
                error = "Parameters must be a JSON object.";
                return false;
            }

            Node["parameters"] = parameters;
            error = null;
            return true;
        }
        catch (JsonException exception)
        {
            error = $"Invalid parameters JSON: {exception.Message}";
            return false;
        }
    }

    public void AddChild()
    {
        EnsureChildrenAttached();

        var node = ScenarioVisualModel.CreateStep(
            StepType.Screenshot);
        _children.Add(node);
        Children.Add(new ScenarioVisualStepModel(node));
    }

    public void RemoveChild(ScenarioVisualStepModel child)
    {
        ArgumentNullException.ThrowIfNull(child);
        _children.Remove(child.Node);
        Children.Remove(child);

        if (Children.Count == 0)
        {
            Node.Remove("children");
        }
    }

    private JsonObject EnsureLocator()
    {
        if (Node["locator"] is JsonObject locator)
        {
            return locator;
        }

        locator = new JsonObject
        {
            ["kind"] = ScenarioLocatorKind.Selector.ToString(),
            ["value"] = string.Empty,
            ["exact"] = false,
        };
        Node["locator"] = locator;
        Node.Remove("selector");
        return locator;
    }

    private void EnsureChildrenAttached()
    {
        if (!ReferenceEquals(Node["children"], _children))
        {
            Node["children"] = _children;
        }
    }
}
