using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class ScenarioExecutorInterpolationTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(
            Path.GetTempPath(),
            "dabot-interpolation-tests",
            Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ExecuteAsync_UsesPriorOutputsAndBuiltInRunIdInLaterSteps()
    {
        var browser = new RecordingBrowserAutomation
        {
            ReadTextValue = "example.com",
        };
        var executor = new ScenarioExecutor(
            new BotOptions
            {
                Storage = new StorageOptions
                {
                    ScreenshotsDirectory = Path.Combine(
                        _tempDirectory,
                        "screenshots"),
                },
            },
            new ScenarioValidationService(),
            [
                new ReadTextStepHandler(),
                new OpenUrlStepHandler(),
            ],
            browser);

        var result = await executor.ExecuteAsync(
            new ScenarioDefinition
            {
                Name = "interpolation-test",
                Steps =
                [
                    new ScenarioStep
                    {
                        Type = StepType.ReadText,
                        Selector = "#host",
                        Output = "host",
                    },
                    new ScenarioStep
                    {
                        Type = StepType.OpenUrl,
                        Url = "https://{{host}}/runs/{{runId}}",
                    },
                ],
            });

        result.Success.Should().BeTrue();
        browser.NavigatedUrl.Should().StartWith("https://example.com/runs/");
        browser.NavigatedUrl.Should().NotContain("{{");
        browser.NavigatedUrl.Should().EndWith("-interpolation-test");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private sealed class RecordingBrowserAutomation : IBrowserAutomation
    {
        public string ReadTextValue { get; init; } = string.Empty;

        public string? NavigatedUrl { get; private set; }

        public Task OpenAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task NavigateAsync(
            string url,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default)
        {
            NavigatedUrl = url;
            return Task.CompletedTask;
        }

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
            Task.FromResult(ReadTextValue);

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

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
