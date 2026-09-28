using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public interface IDurableRunRecoveryService
{
    Task<DurableRunRecoveryResult> RecoverAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
}

public enum DurableRunRecoveryOutcome
{
    NoAction,
    AutomaticResume,
    VerificationRequired,
    HumanDecisionRequired,
    Completed,
    Failed,
}

public sealed record DurableStepRecoveryDecision(
    Guid AttemptId,
    string StepId,
    StepRecoveryAction Action);

public sealed class DurableRunRecoveryResult
{
    public required AutomationRun Run { get; init; }

    public required DurableRunRecoveryOutcome Outcome { get; init; }

    public IReadOnlyList<DurableStepRecoveryDecision> Decisions { get; init; } =
        Array.Empty<DurableStepRecoveryDecision>();
}
