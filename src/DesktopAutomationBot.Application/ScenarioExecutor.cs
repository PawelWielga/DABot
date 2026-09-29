using System.Text.Json;
using DesktopAutomationBot.Core;
using Microsoft.Extensions.Logging;

namespace DesktopAutomationBot.Application;

public sealed class ScenarioExecutor : IScenarioExecutor
{
    private readonly BotOptions _options;
    private readonly IScenarioValidationService _validationService;
    private readonly IReadOnlyDictionary<StepType, IStepHandler> _handlers;
    private readonly IBrowserAutomation _browserAutomation;
    private readonly ILogger<ScenarioExecutor> _logger;

    public ScenarioExecutor(
        BotOptions options,
        IScenarioValidationService validationService,
        IEnumerable<IStepHandler> handlers,
        IBrowserAutomation browserAutomation,
        ILogger<ScenarioExecutor>? logger = null)
    {
        _options = options;
        _validationService = validationService;
        _browserAutomation = browserAutomation;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ScenarioExecutor>.Instance;
        _handlers = handlers.ToDictionary(handler => handler.StepType);
    }

    public async Task<ScenarioExecutionResult> ExecuteAsync(ScenarioDefinition scenario, CancellationToken cancellationToken = default)
    {
        var compiledScenario = ScenarioCompiler.Compile(scenario);
        scenario = ScenarioCompiler.Materialize(compiledScenario);
        _validationService.ValidateOrThrow(scenario);
        ScenarioHandlerValidator.ValidateOrThrow(
            scenario,
            _handlers.Keys);

        var context = new ScenarioExecutionContext(scenario, _browserAutomation, _options);
        Directory.CreateDirectory(context.ScreenshotDirectory);
        Directory.CreateDirectory(context.ArtifactDirectory);

        using var logScope = _logger.BeginScope(
            new Dictionary<string, object?>
            {
                ["RunId"] = context.RunId,
                ["ScenarioName"] = scenario.Name,
            });

        _logger.LogInformation(
            "Starting scenario {ScenarioName} with run {RunId}",
            scenario.Name,
            context.RunId);

        using var timeoutCancellation = scenario.TimeoutMs.HasValue
            ? new CancellationTokenSource(scenario.TimeoutMs.Value)
            : null;
        using var executionCancellation = timeoutCancellation is null
            ? null
            : CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCancellation.Token);
        var executionToken = executionCancellation?.Token ?? cancellationToken;

        var result = new ScenarioExecutionResult
        {
            ScenarioName = scenario.Name,
            RunId = context.RunId,
            Success = false,
        };

        try
        {
            executionToken.ThrowIfCancellationRequested();
            await _browserAutomation.OpenAsync(executionToken);

            await ExecuteStepsAsync(
                scenario.Steps,
                context,
                result,
                executionToken);

            result.Success = true;
            _logger.LogInformation(
                "Scenario {ScenarioName} completed successfully",
                scenario.Name);
            return result;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Scenario {ScenarioName} was cancelled",
                scenario.Name);
            throw;
        }
        catch (OperationCanceledException)
            when (timeoutCancellation?.IsCancellationRequested == true)
        {
            var exception = new TimeoutException(
                $"Scenario '{scenario.Name}' exceeded its timeout of {scenario.TimeoutMs} ms.");
            result.ErrorMessage = exception.Message;
            _logger.LogError(
                exception,
                "Scenario {ScenarioName} timed out after {TimeoutMs} ms",
                scenario.Name,
                scenario.TimeoutMs);
            return result;
        }
        catch (Exception exception)
        {
            result.ErrorMessage = exception.Message;
            _logger.LogError(
                exception,
                "Scenario {ScenarioName} failed",
                scenario.Name);

            try
            {
                var htmlPath = Path.Combine(
                    context.ArtifactDirectory,
                    "failure.html");
                result.FailureHtmlPath =
                    await _browserAutomation.SaveHtmlSnapshotAsync(
                        htmlPath,
                        CancellationToken.None);
            }
            catch
            {
                // Diagnostics must never hide the original execution error.
            }

            try
            {
                var screenshotPath = Path.Combine(
                    context.ArtifactDirectory,
                    "failure.png");
                result.FailureScreenshotPath =
                    await _browserAutomation.TakeScreenshotAsync(
                        screenshotPath,
                        CancellationToken.None);
            }
            catch
            {
                // Diagnostics must never hide the original execution error.
            }

            return result;
        }
        finally
        {
            try
            {
                var reportPath = Path.Combine(
                    context.ArtifactDirectory,
                    "run-report.json");
                result.ReportPath = reportPath;
                var reportJson = JsonSerializer.Serialize(
                    result,
                    new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(
                    reportPath,
                    reportJson,
                    CancellationToken.None);
            }
            catch
            {
                result.ReportPath = null;
                // Diagnostics must never hide the execution outcome.
            }

            await _browserAutomation.DisposeAsync();
        }
    }

    private async Task<StepExecutionResult> ExecuteStepWithRetryAsync(
        ScenarioStep step,
        IStepHandler handler,
        ScenarioExecutionContext context,
        int index,
        CancellationToken cancellationToken)
    {
        var maximumAttempts = StepRetryPolicy.GetMaximumAttempts(step);

        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var resolvedStep = ScenarioVariableInterpolator.Resolve(
                    step,
                    context.Variables);

                return await handler.ExecuteAsync(
                    resolvedStep,
                    context,
                    index,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
                when (attempt < maximumAttempts)
            {
                var delay = StepRetryPolicy.GetRetryDelay(step);
                _logger.LogWarning(
                    exception,
                    "Step {StepIndex} ({StepType}) attempt {Attempt} failed; retrying after {RetryDelayMs} ms",
                    index + 1,
                    step.Type,
                    attempt,
                    delay.TotalMilliseconds);

                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }
    }

    private async Task ExecuteStepsAsync(
        IReadOnlyList<ScenarioStep> steps,
        ScenarioExecutionContext context,
        ScenarioExecutionResult result,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < steps.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = steps[index];

            if (step.Type == StepType.If)
            {
                if (ControlFlowStepEvaluator.EvaluateIf(step, context.Variables))
                {
                    await ExecuteStepsAsync(
                        step.Children,
                        context,
                        result,
                        cancellationToken);
                }

                continue;
            }

            if (step.Type == StepType.Loop)
            {
                var count = ControlFlowStepEvaluator.GetLoopCount(
                    step,
                    context.Variables);

                for (var iteration = 0; iteration < count; iteration++)
                {
                    await ExecuteStepsAsync(
                        step.Children,
                        context,
                        result,
                        cancellationToken);
                }

                continue;
            }

            if (!_handlers.TryGetValue(step.Type, out var handler))
            {
                throw new NotSupportedException(
                    $"Step type '{step.Type}' is not supported yet.");
            }

            _logger.LogInformation(
                "Executing step {StepIndex} ({StepType})",
                index + 1,
                step.Type);

            var stepResult = await ExecuteStepWithRetryAsync(
                step,
                handler,
                context,
                index,
                cancellationToken);
            context.CaptureOutput(stepResult);
            result.Steps.Add(stepResult);

            _logger.LogInformation(
                "Completed step {StepIndex} ({StepType})",
                index + 1,
                step.Type);
        }
    }
}
