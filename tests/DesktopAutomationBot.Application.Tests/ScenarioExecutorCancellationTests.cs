using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class ScenarioExecutorCancellationTests
{
    [Fact]
    public async Task ExecuteAsync_WhenHandlerObservesCancellation_RethrowsAndDisposesBrowser()
    {
        using var cancellation = new CancellationTokenSource();
        var browser = new RecordingBrowserAutomation();
        var handler = new CancellingStepHandler(cancellation);
        var executor = new ScenarioExecutor(
            new BotOptions(),
            new ScenarioValidationService(),
            [handler],
            browser);

        var scenario = new ScenarioDefinition
        {
            Name = "Cancellation test",
            Steps =
            [
                new ScenarioStep
                {
                    Id = "open",
                    Type = StepType.OpenUrl,
                    Url = "https://example.com",
                },
            ],
        };

        Func<Task> action = () =>
            executor.ExecuteAsync(
                scenario,
                cancellation.Token);

        await action.Should()
            .ThrowAsync<OperationCanceledException>();

        browser.OpenCount.Should().Be(1);
        browser.DisposeCount.Should().Be(1);
    }

    private sealed class CancellingStepHandler : IStepHandler
    {
        private readonly CancellationTokenSource _cancellation;

        public CancellingStepHandler(
            CancellationTokenSource cancellation)
        {
            _cancellation = cancellation;
        }

        public StepType StepType => StepType.OpenUrl;

        public Task<StepExecutionResult> ExecuteAsync(
            ScenarioStep step,
            ScenarioExecutionContext context,
            int index,
            CancellationToken cancellationToken)
        {
            _cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();

            throw new InvalidOperationException(
                "Cancellation token was expected to throw.");
        }
    }

    private sealed class RecordingBrowserAutomation : IBrowserAutomation
    {
        public int OpenCount { get; private set; }

        public int DisposeCount { get; private set; }

        public Task OpenAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCount++;
            return Task.CompletedTask;
        }

        public Task NavigateAsync(
            string url,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ClickAsync(
            string selector,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task FillTextAsync(
            string selector,
            string value,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PasteTextAsync(
            string selector,
            string value,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string> ReadTextAsync(
            string selector,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Empty);

        public Task WaitForSelectorAsync(
            string selector,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task WaitForTextAsync(
            string text,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task WaitForUrlAsync(
            string url,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task WaitForLoadStateAsync(
            string loadState,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string> TakeScreenshotAsync(
            string filePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(filePath);

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
