using System.Globalization;
using System.Text.Json;

namespace DesktopAutomationBot.Core;

public sealed class ScenarioVariableValue : IEquatable<ScenarioVariableValue>
{
    private readonly JsonElement _value;

    private ScenarioVariableValue(JsonElement value)
    {
        _value = value.Clone();
    }

    public JsonValueKind Kind => _value.ValueKind;

    public static ScenarioVariableValue FromString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ScenarioVariableValue(
            JsonSerializer.SerializeToElement(value));
    }

    public static ScenarioVariableValue FromBoolean(bool value) =>
        new(JsonSerializer.SerializeToElement(value));

    public static ScenarioVariableValue FromNumber(decimal value) =>
        new(JsonSerializer.SerializeToElement(value));

    public static ScenarioVariableValue FromJsonElement(JsonElement value) =>
        new(value);

    public static ScenarioVariableValue ParseJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json);
        return new ScenarioVariableValue(document.RootElement);
    }

    public JsonElement ToJsonElement() => _value.Clone();

    public string ToInterpolationString() =>
        _value.ValueKind switch
        {
            JsonValueKind.String => _value.GetString() ?? string.Empty,
            JsonValueKind.True => bool.TrueString.ToLowerInvariant(),
            JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
            JsonValueKind.Number => _value.GetRawText(),
            JsonValueKind.Null => string.Empty,
            JsonValueKind.Object or JsonValueKind.Array => _value.GetRawText(),
            _ => _value.ToString(),
        };

    public override string ToString() => ToInterpolationString();

    public bool Equals(ScenarioVariableValue? other) =>
        other is not null &&
        JsonElement.DeepEquals(_value, other._value);

    public override bool Equals(object? obj) =>
        obj is ScenarioVariableValue other && Equals(other);

    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(_value.GetRawText());

    public static implicit operator ScenarioVariableValue(string value) =>
        FromString(value);
}
