namespace DesktopAutomationBot.Application;

public sealed class ScenarioLoadException : Exception
{
    public ScenarioLoadException(string scenarioPath, IReadOnlyList<string> errors)
        : base($"Failed to load scenario '{scenarioPath}'.")
    {
        ScenarioPath = scenarioPath;
        Errors = errors;
    }

    public string ScenarioPath { get; }

    public IReadOnlyList<string> Errors { get; }
}
