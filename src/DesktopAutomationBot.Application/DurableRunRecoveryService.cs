using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public sealed class DurableRunRecoveryService : IDurableRunRecoveryService
{
    private readonly IRunStore _runStore;
    private readonly IStepAttemptStore _stepAttemptStore;
    private readonly TimeProvider _timeProvider;

    public DurableRunRecoveryService(
        IRunStore runStore,
        IStepAttemptStore stepAttemptStore,
        TimeProvider timeProvider)
    {
        _runStore = runStore;
        _stepAttemptStore = stepAttemptStore;
        _timeProvider = timeProvider;
    }

    public async Task<DurableRunRecoveryResult> RecoverAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException(
                "Run ID must not be empty.",
                nameof(runId));
        }

        var stored = await _runStore.LoadAsync(
            runId,
            cancellationToken);

        if (stored is null)
        {
            throw new KeyNotFoundException(
                $"Durable run '{runId}' does not exist.");
        }

        var run = stored.Run;
        var scenarioVersion = stored.ScenarioVersion;

        if (run.State.Status != RunStatus.Running)
        {
            return CreateResult(
                run,
                DurableRunRecoveryOutcome.NoAction);
        }

        var detectedAt = GetRecoveryTimestamp(run);

        if (run.Cursor.IsCompleted)
        {
            var completed = RestoreRun(
                run,
                scenarioVersion,
                run.State.Complete(),
                run.Cursor,
                detectedAt);

            await _runStore.SaveAsync(
                completed,
                scenarioVersion,
                cancellationToken);

            return CreateResult(
                completed,
                DurableRunRecoveryOutcome.Completed);
        }

        var scenario = scenarioVersion.MaterializeDefinition();
        var (currentStep, currentStepIndex) =
            DurableExecutionCursorNavigator.ResolveTopLevelStep(
                scenario,
                run.Cursor);

        var newlyUnknown = await _stepAttemptStore.MarkStartedAttemptsUnknownAsync(
            run.RunId,
            detectedAt,
            cancellationToken);

        var attempts = await _stepAttemptStore.LoadStepAttemptsAsync(
            run.RunId,
            cancellationToken);

        var decisions = newlyUnknown
            .Select(CreateDecision)
            .ToArray();

        if (newlyUnknown.Count > 1 ||
            newlyUnknown.Any(
                attempt => !string.Equals(
                    attempt.StepId,
                    currentStep.Id,
                    StringComparison.OrdinalIgnoreCase)))
        {
            var waitingForHuman = await MoveToWaitingAsync(
                run,
                scenarioVersion,
                RunWaitReason.Human,
                detectedAt,
                cancellationToken);

            return CreateResult(
                waitingForHuman,
                DurableRunRecoveryOutcome.HumanDecisionRequired,
                decisions);
        }

        var latestCurrentAttempt = attempts
            .Where(
                attempt => string.Equals(
                    attempt.StepId,
                    currentStep.Id,
                    StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(attempt => attempt.AttemptNumber)
            .ThenByDescending(attempt => attempt.StartedAt)
            .FirstOrDefault();

        if (latestCurrentAttempt is null)
        {
            var waitingForRetry = await MoveToWaitingAsync(
                run,
                scenarioVersion,
                RunWaitReason.Retry,
                detectedAt,
                cancellationToken);

            return CreateResult(
                waitingForRetry,
                DurableRunRecoveryOutcome.AutomaticResume);
        }

        switch (latestCurrentAttempt.Status)
        {
            case StepAttemptStatus.Unknown:
                return await RecoverUnknownAttemptAsync(
                    run,
                    scenarioVersion,
                    latestCurrentAttempt,
                    decisions,
                    detectedAt,
                    cancellationToken);

            case StepAttemptStatus.Completed:
                return await RecoverCompletedAttemptAsync(
                    run,
                    scenarioVersion,
                    scenario,
                    currentStep,
                    currentStepIndex,
                    latestCurrentAttempt,
                    decisions,
                    detectedAt,
                    cancellationToken);

            case StepAttemptStatus.Failed:
            {
                var failed = RestoreRun(
                    run,
                    scenarioVersion,
                    run.State.Fail(),
                    run.Cursor,
                    detectedAt);

                await _runStore.SaveAsync(
                    failed,
                    scenarioVersion,
                    cancellationToken);

                return CreateResult(
                    failed,
                    DurableRunRecoveryOutcome.Failed,
                    decisions);
            }

            case StepAttemptStatus.Started:
            default:
            {
                var waitingForHuman = await MoveToWaitingAsync(
                    run,
                    scenarioVersion,
                    RunWaitReason.Human,
                    detectedAt,
                    cancellationToken);

                return CreateResult(
                    waitingForHuman,
                    DurableRunRecoveryOutcome.HumanDecisionRequired,
                    decisions);
            }
        }
    }

    private async Task<DurableRunRecoveryResult> RecoverUnknownAttemptAsync(
        AutomationRun run,
        ScenarioVersion scenarioVersion,
        StepAttempt attempt,
        IReadOnlyList<DurableStepRecoveryDecision> existingDecisions,
        DateTimeOffset detectedAt,
        CancellationToken cancellationToken)
    {
        var action = StepAttemptRecoveryPolicy.Decide(attempt);
        var decisions = EnsureDecision(
            existingDecisions,
            attempt,
            action);

        switch (action)
        {
            case StepRecoveryAction.RetryAutomatically:
            {
                var waitingForRetry = await MoveToWaitingAsync(
                    run,
                    scenarioVersion,
                    RunWaitReason.Retry,
                    detectedAt,
                    cancellationToken);

                return CreateResult(
                    waitingForRetry,
                    DurableRunRecoveryOutcome.AutomaticResume,
                    decisions);
            }

            case StepRecoveryAction.VerifyBeforeRetry:
            {
                var waitingForHuman = await MoveToWaitingAsync(
                    run,
                    scenarioVersion,
                    RunWaitReason.Human,
                    detectedAt,
                    cancellationToken);

                return CreateResult(
                    waitingForHuman,
                    DurableRunRecoveryOutcome.VerificationRequired,
                    decisions);
            }

            case StepRecoveryAction.WaitingForHuman:
            case StepRecoveryAction.None:
            default:
            {
                var waitingForHuman = await MoveToWaitingAsync(
                    run,
                    scenarioVersion,
                    RunWaitReason.Human,
                    detectedAt,
                    cancellationToken);

                return CreateResult(
                    waitingForHuman,
                    DurableRunRecoveryOutcome.HumanDecisionRequired,
                    decisions);
            }
        }
    }

    private async Task<DurableRunRecoveryResult> RecoverCompletedAttemptAsync(
        AutomationRun run,
        ScenarioVersion scenarioVersion,
        ScenarioDefinition scenario,
        ScenarioStep currentStep,
        int currentStepIndex,
        StepAttempt attempt,
        IReadOnlyList<DurableStepRecoveryDecision> decisions,
        DateTimeOffset detectedAt,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(currentStep.Output))
        {
            var action = StepAttemptRecoveryPolicy.Decide(
                attempt with
                {
                    Status = StepAttemptStatus.Unknown,
                    FinishedAt = null,
                    ErrorMessage = null,
                });

            if (action == StepRecoveryAction.RetryAutomatically)
            {
                var waitingForRetry = await MoveToWaitingAsync(
                    run,
                    scenarioVersion,
                    RunWaitReason.Retry,
                    detectedAt,
                    cancellationToken);

                return CreateResult(
                    waitingForRetry,
                    DurableRunRecoveryOutcome.AutomaticResume,
                    EnsureDecision(
                        decisions,
                        attempt,
                        action));
            }

            var waitingForHuman = await MoveToWaitingAsync(
                run,
                scenarioVersion,
                RunWaitReason.Human,
                detectedAt,
                cancellationToken);

            return CreateResult(
                waitingForHuman,
                action == StepRecoveryAction.VerifyBeforeRetry
                    ? DurableRunRecoveryOutcome.VerificationRequired
                    : DurableRunRecoveryOutcome.HumanDecisionRequired,
                EnsureDecision(
                    decisions,
                    attempt,
                    action));
        }

        var nextCursor =
            DurableExecutionCursorNavigator.AdvanceTopLevelCursor(
                scenario,
                currentStepIndex);

        if (nextCursor.IsCompleted)
        {
            var completed = RestoreRun(
                run,
                scenarioVersion,
                run.State.Complete(),
                nextCursor,
                detectedAt);

            await _runStore.SaveAsync(
                completed,
                scenarioVersion,
                cancellationToken);

            return CreateResult(
                completed,
                DurableRunRecoveryOutcome.Completed,
                decisions);
        }

        var readyToContinue = RestoreRun(
            run,
            scenarioVersion,
            run.State.Wait(RunWaitReason.Retry),
            nextCursor,
            detectedAt);

        await _runStore.SaveAsync(
            readyToContinue,
            scenarioVersion,
            cancellationToken);

        return CreateResult(
            readyToContinue,
            DurableRunRecoveryOutcome.AutomaticResume,
            decisions);
    }

    private async Task<AutomationRun> MoveToWaitingAsync(
        AutomationRun run,
        ScenarioVersion scenarioVersion,
        RunWaitReason reason,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        var waiting = RestoreRun(
            run,
            scenarioVersion,
            run.State.Wait(reason),
            run.Cursor,
            updatedAt);

        await _runStore.SaveAsync(
            waiting,
            scenarioVersion,
            cancellationToken);

        return waiting;
    }

    private DateTimeOffset GetRecoveryTimestamp(AutomationRun run)
    {
        var now = _timeProvider.GetUtcNow();
        return now < run.UpdatedAt
            ? run.UpdatedAt
            : now;
    }

    private static AutomationRun RestoreRun(
        AutomationRun run,
        ScenarioVersion scenarioVersion,
        RunState state,
        ExecutionCursor cursor,
        DateTimeOffset updatedAt) =>
        AutomationRun.Restore(
            run.RunId,
            scenarioVersion,
            state,
            cursor,
            run.Variables,
            run.CreatedAt,
            updatedAt);

    private static DurableStepRecoveryDecision CreateDecision(
        StepAttempt attempt) =>
        new(
            attempt.AttemptId,
            attempt.StepId,
            StepAttemptRecoveryPolicy.Decide(attempt));

    private static IReadOnlyList<DurableStepRecoveryDecision> EnsureDecision(
        IReadOnlyList<DurableStepRecoveryDecision> decisions,
        StepAttempt attempt,
        StepRecoveryAction action)
    {
        if (decisions.Any(
                decision => decision.AttemptId == attempt.AttemptId))
        {
            return decisions;
        }

        return
        [
            .. decisions,
            new DurableStepRecoveryDecision(
                attempt.AttemptId,
                attempt.StepId,
                action),
        ];
    }

    private static DurableRunRecoveryResult CreateResult(
        AutomationRun run,
        DurableRunRecoveryOutcome outcome,
        IReadOnlyList<DurableStepRecoveryDecision>? decisions = null) =>
        new()
        {
            Run = run,
            Outcome = outcome,
            Decisions = decisions ?? Array.Empty<DurableStepRecoveryDecision>(),
        };
}
