namespace DesktopAutomationBot.Runner;

internal sealed class SensitiveValueMasker
{
    public const string Mask = "***";

    private readonly string[] _values;

    public SensitiveValueMasker(IEnumerable<string> environmentVariables)
    {
        _values = environmentVariables
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(Environment.GetEnvironmentVariable)
            .Where(value => !string.IsNullOrEmpty(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(value => value.Length)
            .ToArray();
    }

    public string? MaskText(string? value)
    {
        if (value is null)
        {
            return null;
        }

        foreach (var sensitiveValue in _values)
        {
            value = value.Replace(
                sensitiveValue,
                Mask,
                StringComparison.Ordinal);
        }

        return value;
    }

    public object? MaskValue(object? value) =>
        value is string text ? MaskText(text) : value;
}
