using System.Text.Json;
using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public sealed class ScenarioExecutor : IScenarioExecutor
{
    private readonly BotOptions _options;
    private readonly IScenarioValidationService _validationService;
    private readonly IReadOnlyDictionary<StepType, IStepHandler> _handlers;
    private readonly IBrowserAutomation _browserAutomation;

    public ScenarioExecutor(
        BotOptions options,
        IScenarioValidationService validationService,
        IEnumerable<IStepHandler> handlers,
        IBrowserAutomation browserAutomation)
    {
        _options = options;
        _validationService = validationService;
        _browserAutomation = browserAutomation;
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
            }

            result.Success = true;
            return result;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            result.ErrorMessage = exception.Message;

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

            await _browserAutomation.DisposeAsync();
        }
    }
}
