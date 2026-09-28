using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public sealed class DurableScenarioExecutor : IDurableScenarioExecutor
{
    private readonly BotOptions _options;
    private readonly IScenarioValidationService _validationService;
    private readonly IReadOnlyDictionary<StepType, IStepHandler> _handlers;
    private readonly IBrowserAutomation _browserAutomation;
    private readonly IRunStore _runStore;
    private readonly IStepAttemptStore _stepAttemptStore;
    private readonly TimeProvider _timeProvider;

    public DurableScenarioExecutor(
        BotOptions options,
        IScenarioValidationService validationService,
        IEnumerable<IStepHandler> handlers,
        IBrowserAutomation browserAutomation,
        IRunStore runStore,
        IStepAttemptStore stepAttemptStore,
        TimeProvider timeProvider)
    {
        _options = options;
        _validationService = validationService;
        _browserAutomation = browserAutomation;
        _runStore = runStore;
        _stepAttemptStore = stepAttemptStore;
        _timeProvider = timeProvider;
        _handlers = handlers.ToDictionary(handler => handler.StepType);
    }

    public async Task<DurableScenarioExecutionResult> ExecuteAsync(
        ScenarioRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ScenarioVersion);

        var scenarioVersion = request.ScenarioVersion;
        var scenario = scenarioVersion.MaterializeDefinition();
        _validationService.ValidateOrThrow(scenario);

        var run = request.CreateRun(_timeProvider.GetUtcNow());
        var variables = new Dictionary<string, string>(
            run.Variables,
            StringComparer.OrdinalIgnoreCase);
        var stepResults = new List<StepExecutionResult>();
        var context = new ScenarioExecutionContext(
            scenario,
            _browserAutomation,
            _options,
            run.RunId,
            variables);

        Directory.CreateDirectory(context.ScreenshotDirectory);

        await _runStore.SaveAsync(
            run,
            scenarioVersion,
            cancellationToken);

        run = RestoreSnapshot(
            run,
            scenarioVersion,
            run.State.Start(),
            run.Cursor,
            variables,
            _timeProvider.GetUtcNow());

        await _runStore.SaveAsync(
            run,
            scenarioVersion,
            cancellationToken);

        try
        {
            await _browserAutomation.OpenAsync(cancellationToken);

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

                if (!_handlers.TryGetValue(step.Type, out var handler))
                {
                    throw new NotSupportedException(
                        $"Step type '{step.Type}' is not supported yet.");
                }

                var startedAttempt = StepAttempt.Start(
                    run.RunId,
                    step,
                    attemptNumber: 1,
                    _timeProvider.GetUtcNow());

                await _stepAttemptStore.SaveStepAttemptAsync(
                    startedAttempt,
                    cancellationToken);

                StepExecutionResult stepResult;

                try
                {
                    stepResult = await handler.ExecuteAsync(
                        step,
                        context,
                        index,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    var unknownAttempt = startedAttempt.MarkUnknown(
                        _timeProvider.GetUtcNow());

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
                        _timeProvider.GetUtcNow());

                    await _stepAttemptStore.SaveStepAttemptAsync(
                        failedAttempt,
                        CancellationToken.None);

                    run = await FailAsync(
                        run,
                        scenarioVersion,
                        variables);

                    return CreateResult(
                        run,
                        DurableExecutionOutcome.Failed,
                        stepResults,
                        exception.Message);
                }

                CaptureOutputVariable(
                    variables,
                    stepResult);

                var completedAttempt = startedAttempt.MarkCompleted(
                    _timeProvider.GetUtcNow());

                await _stepAttemptStore.SaveStepAttemptAsync(
                    completedAttempt,
                    CancellationToken.None);

                stepResults.Add(stepResult);

                var nextCursor =
                    DurableExecutionCursorNavigator.AdvanceTopLevelCursor(
                        scenario,
                        index);

                var nextState = nextCursor.IsCompleted
                    ? run.State.Complete()
                    : run.State;

                run = RestoreSnapshot(
                    run,
                    scenarioVersion,
                    nextState,
                    nextCursor,
                    variables,
                    _timeProvider.GetUtcNow());

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
            await _browserAutomation.DisposeAsync();
        }
    }

    private async Task<AutomationRun> FailAsync(
        AutomationRun run,
        ScenarioVersion scenarioVersion,
        IReadOnlyDictionary<string, string> variables)
    {
        var failed = RestoreSnapshot(
            run,
            scenarioVersion,
            run.State.Fail(),
            run.Cursor,
            variables,
            _timeProvider.GetUtcNow());

        await _runStore.SaveAsync(
            failed,
            scenarioVersion,
            CancellationToken.None);

        return failed;
    }

    private async Task<AutomationRun> CancelAsync(
        AutomationRun run,
        ScenarioVersion scenarioVersion,
        IReadOnlyDictionary<string, string> variables)
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
            _timeProvider.GetUtcNow());

        await _runStore.SaveAsync(
            cancelled,
            scenarioVersion,
            CancellationToken.None);

        return cancelled;
    }

    private static AutomationRun RestoreSnapshot(
        AutomationRun run,
        ScenarioVersion scenarioVersion,
        RunState state,
        ExecutionCursor cursor,
        IReadOnlyDictionary<string, string> variables,
        DateTimeOffset updatedAt) =>
        AutomationRun.Restore(
            run.RunId,
            scenarioVersion,
            state,
            cursor,
            variables,
            run.CreatedAt,
            updatedAt);

    private static void CaptureOutputVariable(
        IDictionary<string, string> variables,
        StepExecutionResult result)
    {
        if (string.IsNullOrWhiteSpace(result.OutputName) ||
            result.OutputValue is null)
        {
            return;
        }

        variables[result.OutputName] = result.OutputValue;
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
