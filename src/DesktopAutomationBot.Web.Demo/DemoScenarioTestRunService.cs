using DesktopAutomationBot.Application;

namespace DesktopAutomationBot.Web.Demo;

public sealed class DemoScenarioTestRunService : IScenarioTestRunService
{
    private const string Message =
        "Scenario test runs are disabled in the static GitHub Pages demo.";

    public Task<ScenarioTestRunResult> RunAsync(
        string json,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(Message);
}
