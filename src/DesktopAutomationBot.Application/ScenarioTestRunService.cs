using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public interface IScenarioTestRunService
{
    Task<ScenarioTestRunResult> RunAsync(
        string json,
        CancellationToken cancellationToken = default);
}

public sealed record ScenarioTestRunResult
{
    public ScenarioExecutionResult? Execution { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];

    public bool Executed => Execution is not null;

    public bool Success => Execution?.Success == true;
}

public sealed class ScenarioTestRunService(
    IScenarioExecutor executor) :
    IScenarioTestRunService
{
    public async Task<ScenarioTestRunResult> RunAsync(
        string json,
        CancellationToken cancellationToken = default)
    {
        var parsed = ScenarioJsonValidation.Parse(json);

        if (!parsed.IsValid || parsed.Definition is null)
        {
            return new ScenarioTestRunResult
            {
                Errors = parsed.Errors,
            };
        }

        if (ContainsSuspend(parsed.Definition.Steps))
        {
            return new ScenarioTestRunResult
            {
                Errors =
                [
                    "Editor test runs use the one-shot execution path and cannot contain Suspend. Durable scenarios must be saved and executed through the durable run path."
                ],
            };
        }

        var execution = await executor.ExecuteAsync(
            parsed.Definition,
            cancellationToken);

        return new ScenarioTestRunResult
        {
            Execution = execution,
        };
    }

    private static bool ContainsSuspend(
        IReadOnlyList<ScenarioStep> steps) =>
        steps.Any(step =>
            step.Type == StepType.Suspend ||
            ContainsSuspend(step.Children));
}
