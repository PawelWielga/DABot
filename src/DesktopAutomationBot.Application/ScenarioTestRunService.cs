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

        var execution = await executor.ExecuteAsync(
            parsed.Definition,
            cancellationToken);

        return new ScenarioTestRunResult
        {
            Execution = execution,
            Errors = string.IsNullOrWhiteSpace(execution.ErrorMessage)
                ? []
                : [execution.ErrorMessage],
        };
    }
}
