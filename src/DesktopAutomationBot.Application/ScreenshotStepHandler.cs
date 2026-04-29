using System.Globalization;
using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public sealed class ScreenshotStepHandler : IStepHandler
{
    public StepType StepType => StepType.Screenshot;

    public async Task<StepExecutionResult> ExecuteAsync(ScenarioStep step, ScenarioExecutionContext context, int index, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(context.ScreenshotDirectory);

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        var fileName = $"{timestamp}-step-{index + 1}.png";
        var filePath = Path.Combine(context.ScreenshotDirectory, fileName);

        await context.BrowserAutomation.TakeScreenshotAsync(filePath, cancellationToken);

        return new StepExecutionResult
        {
            Index = index,
            Type = StepType,
            Success = true,
            ArtifactPath = filePath,
        };
    }
}
