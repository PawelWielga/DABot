using System.Globalization;
using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public sealed class ScenarioExecutionContext
{
    public ScenarioExecutionContext(
        ScenarioDefinition scenario,
        IBrowserAutomation browserAutomation,
        BotOptions options,
        Guid? durableRunId = null,
        IReadOnlyDictionary<string, string>? initialVariables = null)
    {
        Scenario = scenario;
        BrowserAutomation = browserAutomation;
        Options = options;
        RunId = durableRunId?.ToString("D") ?? CreateRunId(scenario.Name);

        if (initialVariables is not null)
        {
            foreach (var (name, value) in initialVariables)
            {
                Variables.Set(name, value);
            }
        }
    }

    public ScenarioDefinition Scenario { get; }

    public IBrowserAutomation BrowserAutomation { get; }

    public BotOptions Options { get; }

    public ScenarioVariableBag Variables { get; } = new();

    public string RunId { get; }

    public string ScreenshotDirectory => Path.Combine(Options.Storage.ScreenshotsDirectory, RunId);

    private static string CreateRunId(string scenarioName)
    {
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        var sanitizedName = new string(scenarioName.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character).ToArray());
        sanitizedName = string.IsNullOrWhiteSpace(sanitizedName) ? "scenario" : sanitizedName.Trim();
        return $"{stamp}-{sanitizedName}";
    }
}
