using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public sealed class DurableScenarioExecutor :
    IDurableScenarioExecutor,
    IDurableRunResumeService,
    IDurableRunControlService
{
    private readonly BotOptions _options;
    private readonly IScenarioValidationService _validationService;
    private readonly IReadOnlyDictionary<StepType, IStepHandler> _handlers;
    private readonly IBrowserSessionFactory _browserSessionFactory;
    private readonly IRunStore _runStore;
    private readonly IStepAttemptStore _stepAttemptStore;
    private readonly TimeProvider _timeProvider;

    public DurableScenarioExecutor(
        BotOptions options,
        IScenarioValidationService validationService,
        IEnumerable<IStepHandler> handlers,
        IBrowserSessionFactory browserSessionFactory,
        IRunStore runStore,
        IStepAttemptStore stepAttemptStore,
        TimeProvider timeProvider)
    {
        _options = options;
        _validationService = validationService;
        _browserSessionFactory = browserSessionFactory;
        _runStore = runStore;
        _stepAttemptStore = stepAttemptStore;
        _timeProvider = timeProvider;
        _handlers = handlers.ToDictionary(handler => handler.StepType);
    }

    public DurableScenarioExecutor(
        BotOptions options,
        IScenarioValidationService validationService,
        IEnumerable<IStepHandler> handlers,
        IBrowserAutomation browserAutomation,
        IRunStore runStore,
        IStepAttemptStore stepAttemptStore,
        TimeProvider timeProvider)
        : this(
            options,
            validationService,
            handlers,
            new FixedBrowserSessionFactory(browserAutomation),
            runStore,
            stepAttemptStore,
            timeProvider)
    {
    }

    public async Task<DurableScenarioExecutionResult> ExecuteAsync(
        ScenarioRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ScenarioVersion);

        var scenarioVersion = request.ScenarioVersion;
        var scenario = ScenarioCompiler.Materialize(
            scenarioVersion.Compile());
        _validationService.ValidateOrThrow(scenario);
        ScenarioHandlerValidator.ValidateOrThrow(
            scenario,
            _handlers.Keys);

        var run = request.CreateRun(_timeProvider.GetUtcNow());

        await _runStore.SaveAsync(
            run,
            scenarioVersion,
            cancellationToken);

        run = RestoreSnapshot(
            run,
            scenarioVersion,
            run.State.Start(),
            run.Cursor,
            run.Variables,
            NextTimestamp(run.UpdatedAt));

        await _runStore.SaveAsync(
            run,
            scenarioVersion,
            cancellationToken);

        return await ExecuteRunningRunAsync(
            run,
            scenarioVersion,
            cancellationToken);
    }

    public async Task<DurableScenarioExecutionResult> ResumeAsync(
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

        if (run.State.Status != RunStatus.Waiting ||
            run.State.WaitReason != RunWaitReason.Retry)
        {
            throw new InvalidOperationException(
                $"Run '{runId}' can be resumed automatically only from 'Waiting / Retry'. " +
                $"Current state is '{run.State.Status}'" +
                (run.State.WaitReason is null
                    ? "."
                    : $" with reason '{run.State.WaitReason}'."));
        }

        if (run.Cursor.IsCompleted)
        {
            throw new InvalidOperationException(
                $"Run '{runId}' cannot resume because its execution cursor is already completed.");
        }

        var attempts = await _stepAttemptStore.LoadStepAttemptsAsync(
            run.RunId,
            cancellationToken);

        if (attempts.Any(attempt => attempt.Status == StepAttemptStatus.Started))
        {
            throw new InvalidOperationException(
                $"Run '{runId}' still contains a started step attempt. " +
                "Crash recovery must reconcile started attempts before resume.");
        }

        var scenario = scenarioVersion.MaterializeDefinition();
        _validationService.ValidateOrThrow(scenario);
        ScenarioHandlerValidator.ValidateOrThrow(
            scenario,
            _handlers.Keys);

        var (step, _) =
            DurableExecutionCursorNavigator.ResolveTopLevelStep(
                scenario,
                run.Cursor);

        if (!DurableRetryPolicy.HasRemainingAttempt(
                step,
                attempts))
        {
            var errorMessage =
                $"Retry limit exhausted for step '{step.Id}'. " +
                $"Configured retryCount is {step.RetryCount ?? 0}.";

            run = await FailAsync(
                run,
                scenarioVersion,
                run.Variables);

            return CreateResult(
                run,
                DurableExecutionOutcome.Failed,
                [],
                errorMessage);
        }

        if (run.RetryNotBefore is { } retryNotBefore &&
            _timeProvider.GetUtcNow() < retryNotBefore)
        {
            return CreateResult(
                run,
                DurableExecutionOutcome.Suspended,
                []);
        }

        run = RestoreSnapshot(
            run,
            scenarioVersion,
            run.State.Resume(),
            run.Cursor,
            run.Variables,
            NextTimestamp(run.UpdatedAt));

        await _runStore.SaveAsync(
            run,
            scenarioVersion,
            cancellationToken);

        return await ExecuteRunningRunAsync(
            run,
            scenarioVersion,
            cancellationToken);
    }

    public async Task<DurableScenarioExecutionResult> ResumeManuallyAsync(
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

        if (run.State.Status != RunStatus.Waiting)
        {
            throw new InvalidOperationException(
                $"Run '{runId}' can be resumed manually only from Waiting. Current state is '{run.State.Status}'.");
        }

        if (run.State.WaitReason == RunWaitReason.Retry)
        {
            throw new InvalidOperationException(
                $"Run '{runId}' is waiting for Retry and must use the normal retry resume path.");
        }

        var attempts = await _stepAttemptStore.LoadStepAttemptsAsync(
            run.RunId,
            cancellationToken);

        if (attempts.Any(attempt => attempt.Status == StepAttemptStatus.Started))
        {
            throw new InvalidOperationException(
                $"Run '{runId}' still contains a started step attempt. Crash recovery must reconcile started attempts before resume.");
        }

        var scenario = scenarioVersion.MaterializeDefinition();
        _validationService.ValidateOrThrow(scenario);
        ScenarioHandlerValidator.ValidateOrThrow(
            scenario,
            _handlers.Keys);

        run = RestoreSnapshot(
            run,
            scenarioVersion,
            run.State.Resume(),
            run.Cursor,
            run.Variables,
            NextTimestamp(run.UpdatedAt));

        await _runStore.SaveAsync(
            run,
            scenarioVersion,
            cancellationToken);

        return await ExecuteRunningRunAsync(
            run,
            scenarioVersion,
            cancellationToken);
    }

    public async Task<AutomationRun> CancelAsync(
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
        if (run.State.IsTerminal)
        {
            return run;
        }

        if (run.State.Status == RunStatus.Running)
        {
            throw new InvalidOperationException(
                $"Run '{runId}' is currently Running. Cross-process cancellation of active runs requires worker lease/CAS coordination and is not safe yet.");
        }

        return await CancelAsync(
            run,
            stored.ScenarioVersion,
            run.Variables);
    }

    private async Task<DurableScenarioExecutionResult> ExecuteRunningRunAsync(
        AutomationRun run,
        ScenarioVersion scenarioVersion,
        CancellationToken cancellationToken)
    {
        if (run.State.Status != RunStatus.Running)
        {
            throw new InvalidOperationException(
                $"Durable execution requires a running run. Current state is '{run.State.Status}'.");
        }

        var scenario = scenarioVersion.MaterializeDefinition();
        var variables = run.Variables.ToDictionary(
            pair => pair.Key,
            pair => ScenarioVariableValue.FromJsonElement(
                pair.Value.ToJsonElement()),
            StringComparer.OrdinalIgnoreCase);
        var stepResults = new List<StepExecutionResult>();
        var browserSession = await _browserSessionFactory.CreateAsync(
            new BrowserSessionRequest
            {
                ProfileName = scenario.BrowserProfile,
            },
            cancellationToken);
        var context = new ScenarioExecutionContext(
            scenario,
            browserSession,
            _options,
            run.RunId,
            variables);

        Directory.CreateDirectory(context.ScreenshotDirectory);

        try
        {
            await browserSession.OpenAsync(cancellationToken);

            while (!run.Cursor.IsCompleted)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    run = await CancelAsync(
                        run,
                        scenarioVersion,
                        variables);

                    return CreateResult(
                        run,
                        DurableExecutionOutcome.Cancelled,
                        stepResults);
                }

                var (step, index) =
                    DurableExecutionCursorNavigator.ResolveTopLevelStep(
                        scenario,
                        run.Cursor);

                if (step.Type is StepType.If or StepType.Loop)
                {
                    ExecutionCursor controlCursor;

                    if (step.Type == StepType.If)
                    {
                        controlCursor = ControlFlowStepEvaluator.EvaluateIf(
                            step,
                            context.Variables)
                            ? DurableExecutionCursorNavigator.EnterIf(
                                step,
                                run.Cursor)
                            : DurableExecutionCursorNavigator.AdvanceCursor(
                                scenario,
                                run.Cursor,
                                index,
                                context.Variables);
                    }
                    else
                    {
                        var count = ControlFlowStepEvaluator.GetLoopCount(
                            step,
                            context.Variables);
                        controlCursor = count > 0
                            ? DurableExecutionCursorNavigator.EnterLoop(
                                step,
                                run.Cursor)
                            : DurableExecutionCursorNavigator.AdvanceCursor(
                                scenario,
                                run.Cursor,
                                index,
                                context.Variables);
                    }

                    var controlState = controlCursor.IsCompleted
                        ? run.State.Complete()
                        : run.State;

                    run = RestoreSnapshot(
                        run,
                        scenarioVersion,
                        controlState,
                        controlCursor,
                        variables,
                        NextTimestamp(run.UpdatedAt));

                    await _runStore.SaveAsync(
                        run,
                        scenarioVersion,
                        CancellationToken.None);

                    continue;
                }

                if (!_handlers.TryGetValue(step.Type, out var handler))
                {
                    throw new NotSupportedException(
                        $"Step type '{step.Type}' is not supported yet.");
                }

                var attempts =
                    await _stepAttemptStore.LoadStepAttemptsAsync(
                        run.RunId,
                        cancellationToken);

                var attemptNumber =
                    DurableRetryPolicy.GetNextAttemptNumber(
                        step,
                        attempts);

                var startedAttempt = StepAttempt.Start(
                    run.RunId,
                    step,
                    attemptNumber,
                    NextTimestamp(run.UpdatedAt));

                await _stepAttemptStore.SaveStepAttemptAsync(
                    startedAttempt,
                    CancellationToken.None);

                StepExecutionResult stepResult;

                try
                {
                    var resolvedStep = ScenarioVariableInterpolator.Resolve(
                        step,
                        context.Variables);
                    stepResult = await handler.ExecuteAsync(
                        resolvedStep,
                        context,
                        index,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    var unknownAttempt = startedAttempt.MarkUnknown(
                        NextTimestamp(startedAttempt.UpdatedAt));

                    await _stepAttemptStore.SaveStepAttemptAsync(
                        unknownAttempt,
                        CancellationToken.None);

                    run = await CancelAsync(
                        run,
                        scenarioVersion,
                        variables);

                    return CreateResult(
                        run,
                        DurableExecutionOutcome.Cancelled,
                        stepResults);
                }
                catch (Exception exception)
                {
                    var failedAttempt = startedAttempt.MarkFailed(
                        exception.Message,
                        NextTimestamp(startedAttempt.UpdatedAt));

                    await _stepAttemptStore.SaveStepAttemptAsync(
                        failedAttempt,
                        CancellationToken.None);

                    return await HandleFailedAttemptAsync(
                        run,
                        scenarioVersion,
                        step,
                        variables,
                        stepResults,
                        exception.Message);
                }

                context.CaptureOutput(stepResult);
                CaptureOutputVariable(
                    variables,
                    stepResult);

                var completedAttempt = startedAttempt.MarkCompleted(
                    NextTimestamp(startedAttempt.UpdatedAt));

                await _stepAttemptStore.SaveStepAttemptAsync(
                    completedAttempt,
                    CancellationToken.None);

                stepResults.Add(stepResult);

                var nextCursor =
                    DurableExecutionCursorNavigator.AdvanceCursor(
                        scenario,
                        run.Cursor,
                        index,
                        context.Variables);

                var nextState = nextCursor.IsCompleted
                    ? run.State.Complete()
                    : run.State;

                run = RestoreSnapshot(
                    run,
                    scenarioVersion,
                    nextState,
                    nextCursor,
                    variables,
                    NextTimestamp(run.UpdatedAt));

                await _runStore.SaveAsync(
                    run,
                    scenarioVersion,
                    CancellationToken.None);
            }

            return CreateResult(
                run,
                DurableExecutionOutcome.Completed,
                stepResults);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            run = await CancelAsync(
                run,
                scenarioVersion,
                variables);

            return CreateResult(
                run,
                DurableExecutionOutcome.Cancelled,
                stepResults);
        }
        catch (Exception exception)
        {
            if (!run.State.IsTerminal)
            {
                run = await FailAsync(
                    run,
                    scenarioVersion,
                    variables);
            }

            return CreateResult(
                run,
                DurableExecutionOutcome.Failed,
                stepResults,
                exception.Message);
        }
        finally
        {
            await browserSession.DisposeAsync();
        }
    }

    private async Task<DurableScenarioExecutionResult> HandleFailedAttemptAsync(
        AutomationRun run,
        ScenarioVersion scenarioVersion,
        ScenarioStep step,
        IReadOnlyDictionary<string, ScenarioVariableValue> variables,
        IReadOnlyList<StepExecutionResult> stepResults,
        string errorMessage)
    {
        var attempts = await _stepAttemptStore.LoadStepAttemptsAsync(
            run.RunId,
            CancellationToken.None);

        if (!DurableRetryPolicy.HasRemainingAttempt(
                step,
                attempts))
        {
            var failed = await FailAsync(
                run,
                scenarioVersion,
                variables);

            return CreateResult(
                failed,
                DurableExecutionOutcome.Failed,
                stepResults,
                errorMessage);
        }

        var action = DurableRetryPolicy.GetRecoveryAction(step);

        switch (action)
        {
            case StepRecoveryAction.RetryAutomatically:
            {
                var waiting = await WaitAsync(
                    run,
                    scenarioVersion,
                    variables,
                    RunWaitReason.Retry,
                    step);

                return CreateResult(
                    waiting,
                    DurableExecutionOutcome.Suspended,
                    stepResults,
                    errorMessage);
            }

            case StepRecoveryAction.VerifyBeforeRetry:
            case StepRecoveryAction.WaitingForHuman:
            {
                var waiting = await WaitAsync(
                    run,
                    scenarioVersion,
                    variables,
                    RunWaitReason.Human);

                return CreateResult(
                    waiting,
                    DurableExecutionOutcome.Suspended,
                    stepResults,
                    errorMessage);
            }

            case StepRecoveryAction.None:
            default:
            {
                var failed = await FailAsync(
                    run,
                    scenarioVersion,
                    variables);

                return CreateResult(
                    failed,
                    DurableExecutionOutcome.Failed,
                    stepResults,
                    errorMessage);
            }
        }
    }

    private async Task<AutomationRun> WaitAsync(
        AutomationRun run,
        ScenarioVersion scenarioVersion,
        IReadOnlyDictionary<string, ScenarioVariableValue> variables,
        RunWaitReason reason,
        ScenarioStep? retryStep = null)
    {
        var updatedAt = NextTimestamp(run.UpdatedAt);
        DateTimeOffset? retryNotBefore = reason == RunWaitReason.Retry
            ? DurableRetryPolicy.GetRetryNotBefore(
                retryStep ?? throw new ArgumentNullException(nameof(retryStep)),
                updatedAt)
            : null;

        var waiting = RestoreSnapshot(
            run,
            scenarioVersion,
            run.State.Wait(reason),
            run.Cursor,
            variables,
            updatedAt,
            retryNotBefore);

        await _runStore.SaveAsync(
            waiting,
            scenarioVersion,
            CancellationToken.None);

        return waiting;
    }

    private async Task<AutomationRun> FailAsync(
        AutomationRun run,
        ScenarioVersion scenarioVersion,
        IReadOnlyDictionary<string, ScenarioVariableValue> variables)
    {
        var failed = RestoreSnapshot(
            run,
            scenarioVersion,
            run.State.Fail(),
            run.Cursor,
            variables,
            NextTimestamp(run.UpdatedAt));

        await _runStore.SaveAsync(
            failed,
            scenarioVersion,
            CancellationToken.None);

        return failed;
    }

    private async Task<AutomationRun> CancelAsync(
        AutomationRun run,
        ScenarioVersion scenarioVersion,
        IReadOnlyDictionary<string, ScenarioVariableValue> variables)
    {
        if (run.State.IsTerminal)
        {
            return run;
        }

        var cancelled = RestoreSnapshot(
            run,
            scenarioVersion,
            run.State.Cancel(),
            run.Cursor,
            variables,
            NextTimestamp(run.UpdatedAt));

        await _runStore.SaveAsync(
            cancelled,
            scenarioVersion,
            CancellationToken.None);

        return cancelled;
    }

    private DateTimeOffset NextTimestamp(
        DateTimeOffset minimum)
    {
        var now = _timeProvider.GetUtcNow();
        return now < minimum
            ? minimum
            : now;
    }

    private static AutomationRun RestoreSnapshot(
        AutomationRun run,
        ScenarioVersion scenarioVersion,
        RunState state,
        ExecutionCursor cursor,
        IReadOnlyDictionary<string, ScenarioVariableValue> variables,
        DateTimeOffset updatedAt,
        DateTimeOffset? retryNotBefore = null) =>
        AutomationRun.RestoreStructured(
            run.RunId,
            scenarioVersion,
            state,
            cursor,
            variables,
            run.CreatedAt,
            updatedAt,
            retryNotBefore);

    private static void CaptureOutputVariable(
        IDictionary<string, ScenarioVariableValue> variables,
        StepExecutionResult result)
    {
        if (string.IsNullOrWhiteSpace(result.OutputName))
        {
            return;
        }

        if (result.OutputVariableValue is not null)
        {
            variables[result.OutputName] = result.OutputVariableValue;
            return;
        }

        if (result.OutputValue is not null)
        {
            variables[result.OutputName] =
                ScenarioVariableValue.FromString(result.OutputValue);
        }
    }

    private static DurableScenarioExecutionResult CreateResult(
        AutomationRun run,
        DurableExecutionOutcome outcome,
        IEnumerable<StepExecutionResult> stepResults,
        string? errorMessage = null) =>
        new()
        {
            Run = run,
            Outcome = outcome,
            Steps = [.. stepResults],
            ErrorMessage = errorMessage,
        };
}
