using System.Text.Json;
using System.Text.RegularExpressions;

namespace DesktopAutomationBot.Core;

public static class ScenarioVariableInterpolator
{
    private static readonly Regex VariableReferencePattern = new(
        @"\{\{\s*(?<name>[A-Za-z0-9_.-]+)\s*\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static ScenarioStep Resolve(
        ScenarioStep step,
        ScenarioVariableBag variables)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(variables);

        return step with
        {
            Selector = ResolveText(step.Selector, variables),
            Url = ResolveText(step.Url, variables),
            Value = ResolveText(step.Value, variables),
            Parameters = ResolveParameters(step.Parameters, variables),
            Children = step.Children
                .Select(child => Resolve(child, variables))
                .ToList(),
        };
    }

    public static string? ResolveText(
        string? value,
        ScenarioVariableBag variables)
    {
        ArgumentNullException.ThrowIfNull(variables);

        if (value is null)
        {
            return null;
        }

        return VariableReferencePattern.Replace(
            value,
            match =>
            {
                var name = match.Groups["name"].Value;

                if (!variables.TryGetValue(name, out var resolvedValue))
                {
                    throw new InvalidOperationException(
                        $"Scenario variable '{name}' is not defined.");
                }

                return resolvedValue;
            });
    }

    public static bool IsExactVariableReference(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var match = VariableReferencePattern.Match(value);

        return match.Success &&
               match.Index == 0 &&
               match.Length == value.Length;
    }

    private static Dictionary<string, JsonElement>? ResolveParameters(
        IReadOnlyDictionary<string, JsonElement>? parameters,
        ScenarioVariableBag variables)
    {
        if (parameters is null)
        {
            return null;
        }

        return parameters.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ValueKind == JsonValueKind.String
                ? JsonSerializer.SerializeToElement(
                    ResolveText(pair.Value.GetString(), variables))
                : pair.Value.Clone(),
            StringComparer.Ordinal);
    }
}
