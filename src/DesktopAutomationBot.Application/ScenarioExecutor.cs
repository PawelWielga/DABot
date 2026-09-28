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

        var result = new ScenarioExecutionResult
        {
            ScenarioName = scenario.Name,
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
            return result;
        }
        finally
        {
            await _browserAutomation.DisposeAsync();
        }
    }
}
