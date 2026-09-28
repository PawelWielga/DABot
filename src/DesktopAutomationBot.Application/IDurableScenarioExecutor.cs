using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public interface IDurableScenarioExecutor
{
    Task<DurableScenarioExecutionResult> ExecuteAsync(
        ScenarioRunRequest request,
        CancellationToken cancellationToken = default);
}

public enum DurableExecutionOutcome
{
    Completed,
    Suspended,
    Failed,
    Cancelled,
}

public sealed class DurableScenarioExecutionResult
{
    public required AutomationRun Run { get; init; }

    public required DurableExecutionOutcome Outcome { get; init; }

    public List<StepExecutionResult> Steps { get; init; } = [];

    public string? ErrorMessage { get; init; }

    public bool Success => Outcome == DurableExecutionOutcome.Completed;
}
