namespace DesktopAutomationBot.Core;

public enum ScenarioLocatorKind
{
    Selector,
    Text,
    TestId,
}

public sealed record ScenarioLocator
{
    public ScenarioLocatorKind Kind { get; init; } =
        ScenarioLocatorKind.Selector;

    public string Value { get; init; } = string.Empty;

    public bool Exact { get; init; }

    public static ScenarioLocator FromSelector(string selector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);

        return new ScenarioLocator
        {
            Kind = ScenarioLocatorKind.Selector,
            Value = selector,
        };
    }
}

public static class ScenarioStepLocator
{
    public static ScenarioLocator Resolve(ScenarioStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        if (step.Locator is not null)
        {
            return step.Locator;
        }

        if (!string.IsNullOrWhiteSpace(step.Selector))
        {
            return ScenarioLocator.FromSelector(step.Selector);
        }

        throw new InvalidOperationException(
            $"Step '{step.Id ?? step.Type.ToString()}' does not define a locator or selector.");
    }
}
