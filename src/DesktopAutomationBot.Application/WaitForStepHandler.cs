using System.Text.Json;
using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public sealed class WaitForStepHandler : IStepHandler
{
    public StepType StepType => StepType.WaitFor;

    public async Task<StepExecutionResult> ExecuteAsync(ScenarioStep step, ScenarioExecutionContext context, int index, CancellationToken cancellationToken)
    {
        var mode = GetParameter(step, "mode")?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(mode))
        {
            if (step.Locator is not null ||
                !string.IsNullOrWhiteSpace(step.Selector))
            {
                mode = "selector";
            }
            else if (!string.IsNullOrWhiteSpace(step.Value))
            {
                mode = "text";
            }
        }

        switch (mode)
        {
            case "selector":
                await context.BrowserAutomation.WaitForLocatorAsync(
                    ScenarioStepLocator.Resolve(step),
                    step.TimeoutMs,
                    cancellationToken);
                break;
            case "url":
                await context.BrowserAutomation.WaitForUrlAsync(step.Value ?? step.Url ?? string.Empty, step.TimeoutMs, cancellationToken);
                break;
            case "loadstate":
                await context.BrowserAutomation.WaitForLoadStateAsync(step.Value ?? string.Empty, step.TimeoutMs, cancellationToken);
                break;
            case "text":
                await context.BrowserAutomation.WaitForTextAsync(step.Value!, step.TimeoutMs, cancellationToken);
                break;
            default:
                if (step.Locator is not null ||
                    !string.IsNullOrWhiteSpace(step.Selector))
                {
                    await context.BrowserAutomation.WaitForLocatorAsync(
                        ScenarioStepLocator.Resolve(step),
                        step.TimeoutMs,
                        cancellationToken);
                    break;
                }

                if (!string.IsNullOrWhiteSpace(step.Value))
                {
                    await context.BrowserAutomation.WaitForTextAsync(step.Value, step.TimeoutMs, cancellationToken);
                    break;
                }

                throw new InvalidOperationException("WaitFor step requires a supported mode, selector or value.");
        }

        return new StepExecutionResult
        {
            Index = index,
            Type = StepType,
            Success = true,
        };
    }

    private static string? GetParameter(ScenarioStep step, string name)
    {
        if (step.Parameters is null || !step.Parameters.TryGetValue(name, out var parameter))
        {
            return null;
        }

        return parameter.ValueKind switch
        {
            JsonValueKind.String => parameter.GetString(),
            JsonValueKind.Number => parameter.ToString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => parameter.GetRawText(),
        };
    }
}
