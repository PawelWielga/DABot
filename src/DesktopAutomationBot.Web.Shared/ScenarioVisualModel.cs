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

        if (GetNode(root, "steps") is JsonArray existingSteps)
        {
            _steps = existingSteps;
        }
        else
        {
            _steps = new JsonArray();
            SetNode(_root, "steps", _steps);
        }

        Steps = _steps
            .OfType<JsonObject>()
            .Select(node => new ScenarioVisualStepModel(node))
            .ToList();
    }

    public List<ScenarioVisualStepModel> Steps { get; }

    public string Name
    {
        get => GetString(_root, "name") ?? string.Empty;
        set => SetNode(_root, "name", JsonValue.Create(value));
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
                GetNode(root, "steps") is not JsonArray steps ||
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

    internal static string? FindPropertyName(
        JsonObject node,
        string propertyName) =>
        node
            .Select(property => property.Key)
            .FirstOrDefault(key =>
                string.Equals(
                    key,
                    propertyName,
                    StringComparison.OrdinalIgnoreCase));

    internal static JsonNode? GetNode(
        JsonObject node,
        string propertyName)
    {
        var key = FindPropertyName(node, propertyName);
        return key is null
            ? null
            : node[key];
    }

    internal static void SetNode(
        JsonObject node,
        string propertyName,
        JsonNode? value)
    {
        var key = FindPropertyName(node, propertyName)
            ?? propertyName;
        node[key] = value;
    }

    internal static bool RemoveNode(
        JsonObject node,
        string propertyName)
    {
        var key = FindPropertyName(node, propertyName);
        return key is not null && node.Remove(key);
    }

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

    public bool MoveStep(
        ScenarioVisualStepModel step,
        int offset) =>
        MoveStep(
            Steps,
            _steps,
            step,
            offset);

    internal static bool MoveStep(
        IList<ScenarioVisualStepModel> models,
        JsonArray nodes,
        ScenarioVisualStepModel step,
        int offset)
    {
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(step);

        if (offset is not (-1 or 1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                offset,
                "Step move offset must be -1 or 1.");
        }

        var currentIndex = models.IndexOf(step);
        if (currentIndex < 0)
        {
            return false;
        }

        var targetIndex = currentIndex + offset;
        if (targetIndex < 0 || targetIndex >= models.Count)
        {
            return false;
        }

        nodes.RemoveAt(currentIndex);
        nodes.Insert(targetIndex, step.Node);

        models.RemoveAt(currentIndex);
        models.Insert(targetIndex, step);

        return true;
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
        GetNode(node, propertyName) is JsonValue value &&
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
            RemoveNode(node, propertyName);
            return;
        }

        SetNode(node, propertyName, JsonValue.Create(value));
    }

    internal static int? GetInt32(
        JsonObject node,
        string propertyName) =>
        GetNode(node, propertyName) is JsonValue value &&
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
            SetNode(node, propertyName, JsonValue.Create(value.Value));
        }
        else
        {
            RemoveNode(node, propertyName);
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
            SetNode(node, propertyName, JsonValue.Create(value.Value.ToString()));
        }
        else
        {
            RemoveNode(node, propertyName);
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
        _children = ScenarioVisualModel.GetNode(node, "children") as JsonArray ?? new JsonArray();

        if (ScenarioVisualModel.GetNode(node, "children") is JsonArray)
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
        set => ScenarioVisualModel.SetNode(
            Node,
            "type",
            JsonValue.Create(value.ToString()));
    }

    public bool Enabled
    {
        get =>
            ScenarioVisualModel.GetNode(Node, "enabled") is not JsonValue value ||
            !value.TryGetValue<bool>(out var enabled) ||
            enabled;
        set
        {
            if (value)
            {
                ScenarioVisualModel.RemoveNode(Node, "enabled");
            }
            else
            {
                ScenarioVisualModel.SetNode(
                    Node,
                    "enabled",
                    JsonValue.Create(false));
            }
        }
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
        ScenarioVisualModel.GetNode(Node, "locator") is JsonObject;

    public ScenarioLocatorKind LocatorKind
    {
        get => ScenarioVisualModel.GetNode(Node, "locator") is JsonObject locator
            ? ScenarioVisualModel.GetEnum<ScenarioLocatorKind>(locator, "kind")
                ?? ScenarioLocatorKind.Selector
            : ScenarioLocatorKind.Selector;
        set
        {
            var locator = EnsureLocator();
            ScenarioVisualModel.SetNode(locator, "kind", JsonValue.Create(value.ToString()));
        }
    }

    public string? LocatorValue
    {
        get => ScenarioVisualModel.GetNode(Node, "locator") is JsonObject locator
            ? ScenarioVisualModel.GetString(locator, "value")
            : null;
        set
        {
            var locator = EnsureLocator();
            ScenarioVisualModel.SetNode(locator, "value", JsonValue.Create(value ?? string.Empty));
        }
    }

    public bool LocatorExact
    {
        get =>
            ScenarioVisualModel.GetNode(Node, "locator") is JsonObject locator &&
            ScenarioVisualModel.GetNode(locator, "exact") is JsonValue value &&
            value.TryGetValue<bool>(out var exact) &&
            exact;
        set
        {
            var locator = EnsureLocator();
            ScenarioVisualModel.SetNode(locator, "exact", JsonValue.Create(value));
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
        ScenarioVisualModel.GetNode(Node, "parameters") is not null;

    public bool SupportsChildren =>
        Type is StepType.If or StepType.Loop ||
        Children.Count > 0;

    public void UseStructuredLocator()
    {
        var selector = Selector;
        ScenarioVisualModel.RemoveNode(Node, "selector");

        ScenarioVisualModel.SetNode(Node, "locator", new JsonObject
        {
            ["kind"] = ScenarioLocatorKind.Selector.ToString(),
            ["value"] = selector ?? string.Empty,
            ["exact"] = false,
        });
    }

    public void UseSelector()
    {
        if (ScenarioVisualModel.GetNode(Node, "locator") is JsonObject locator &&
            ScenarioVisualModel.GetEnum<ScenarioLocatorKind>(locator, "kind") ==
                ScenarioLocatorKind.Selector)
        {
            Selector = ScenarioVisualModel.GetString(locator, "value");
        }

        ScenarioVisualModel.RemoveNode(Node, "locator");
    }

    public string GetParametersJson() =>
        ScenarioVisualModel.GetNode(Node, "parameters") is JsonObject parameters
            ? parameters.ToJsonString(IndentedJson)
            : string.Empty;

    public bool TrySetParametersJson(
        string? json,
        out string? error)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            ScenarioVisualModel.RemoveNode(Node, "parameters");
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

            ScenarioVisualModel.SetNode(Node, "parameters", parameters);
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
            ScenarioVisualModel.RemoveNode(Node, "children");
        }
    }

    public bool MoveChild(
        ScenarioVisualStepModel child,
        int offset)
    {
        EnsureChildrenAttached();

        return ScenarioVisualModel.MoveStep(
            Children,
            _children,
            child,
            offset);
    }

    private JsonObject EnsureLocator()
    {
        if (ScenarioVisualModel.GetNode(Node, "locator") is JsonObject locator)
        {
            return locator;
        }

        locator = new JsonObject
        {
            ["kind"] = ScenarioLocatorKind.Selector.ToString(),
            ["value"] = string.Empty,
            ["exact"] = false,
        };
        ScenarioVisualModel.SetNode(Node, "locator", locator);
        ScenarioVisualModel.RemoveNode(Node, "selector");
        return locator;
    }

    private void EnsureChildrenAttached()
    {
        if (!ReferenceEquals(
                ScenarioVisualModel.GetNode(Node, "children"),
                _children))
        {
            ScenarioVisualModel.SetNode(Node, "children", _children);
        }
    }
}
