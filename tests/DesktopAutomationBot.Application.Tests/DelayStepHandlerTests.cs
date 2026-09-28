using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class DelayStepHandlerTests
{
    [Theory]
    [InlineData(0, null)]
    [InlineData(null, "0")]
    public async Task ExecuteAsync_WithSupportedDuration_ReturnsSuccessfulResult(
        int? timeoutMs,
        string? value)
    {
        var step = new ScenarioStep
        {
            Type = StepType.Delay,
            TimeoutMs = timeoutMs,
            Value = value,
        };
        var context = new ScenarioExecutionContext(
            new ScenarioDefinition
            {
                Name = "Delay test",
                Steps = [step],
            },
            new NoOpBrowserAutomation(),
            new BotOptions());
        var handler = new DelayStepHandler();

        var result = await handler.ExecuteAsync(
            step,
            context,
            0,
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Index.Should().Be(0);
        result.Type.Should().Be(StepType.Delay);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_PropagatesCancellation()
    {
        var step = new ScenarioStep
        {
            Type = StepType.Delay,
            TimeoutMs = 1000,
        };
        var context = new ScenarioExecutionContext(
            new ScenarioDefinition
            {
                Name = "Cancelled delay",
                Steps = [step],
            },
            new NoOpBrowserAutomation(),
            new BotOptions());
        var handler = new DelayStepHandler();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Func<Task> action = () => handler.ExecuteAsync(
            step,
            context,
            0,
            cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    private sealed class NoOpBrowserAutomation : IBrowserAutomation
    {
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

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
