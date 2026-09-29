using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class ScenarioExecutorTimeoutTests
{
    [Fact]
    public async Task ExecuteAsync_WhenScenarioTimeoutExpires_ReturnsTimeoutFailureAndDisposesBrowser()
    {
        var browser = new RecordingBrowserAutomation();
        var executor = new ScenarioExecutor(
            new BotOptions(),
            new ScenarioValidationService(),
            [new BlockingStepHandler()],
            browser);

        var scenario = new ScenarioDefinition
        {
            Name = "Timeout test",
            TimeoutMs = 25,
            Steps =
            [
                new ScenarioStep
                {
                    Type = StepType.OpenUrl,
                    Url = "https://example.com",
                },
            ],
        };

        var result = await executor.ExecuteAsync(scenario);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be(
            "Scenario 'Timeout test' exceeded its timeout of 25 ms.");
        browser.DisposeCount.Should().Be(1);
    }

    private sealed class BlockingStepHandler : IStepHandler
    {
        public StepType StepType => StepType.OpenUrl;

        public async Task<StepExecutionResult> ExecuteAsync(
            ScenarioStep step,
            ScenarioExecutionContext context,
            int index,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Delay should have been cancelled.");
        }
    }

    private sealed class RecordingBrowserAutomation : IBrowserAutomation
    {
        public int DisposeCount { get; private set; }

        public Task OpenAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task NavigateAsync(string url, int? timeoutMs = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task ClickAsync(string selector, int? timeoutMs = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task FillTextAsync(string selector, string value, int? timeoutMs = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task PasteTextAsync(string selector, string value, int? timeoutMs = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task<string> ReadTextAsync(string selector, int? timeoutMs = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Empty);
        public Task WaitForSelectorAsync(string selector, int? timeoutMs = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task WaitForTextAsync(string text, int? timeoutMs = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task WaitForUrlAsync(string url, int? timeoutMs = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task WaitForLoadStateAsync(string loadState, int? timeoutMs = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task<string> TakeScreenshotAsync(string filePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(filePath);

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
