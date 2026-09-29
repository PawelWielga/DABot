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
        scenario = ScenarioDefinitionNormalizer.Normalize(scenario);
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

        var result = new ScenarioExecutionResult
        {
            ScenarioName = scenario.Name,
            RunId = context.RunId,
            Success = false,
        };

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _browserAutomation.OpenAsync(cancellationToken);

            for (var index = 0; index < scenario.Steps.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var step = scenario.Steps[index];
                if (!_handlers.TryGetValue(step.Type, out var handler))
                {
                    throw new NotSupportedException($"Step type '{step.Type}' is not supported yet.");
                }

                _logger.LogInformation(
                    "Executing step {StepIndex} ({StepType})",
                    index + 1,
                    step.Type);

                var resolvedStep = ScenarioVariableInterpolator.Resolve(
                    step,
                    context.Variables);
                var stepResult = await handler.ExecuteAsync(
                    resolvedStep,
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
}
