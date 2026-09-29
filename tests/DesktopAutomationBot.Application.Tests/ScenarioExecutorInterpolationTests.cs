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
    public async Task ExecuteAsync_UsesStepOutputsAndBuiltInRunIdInLaterSteps()
    {
        var browser = new RecordingBrowserAutomation();
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
                new OutputStepHandler("example.com"),
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

    [Fact]
    public async Task ExecuteAsync_WhenStepFails_CapturesFailureScreenshotAndWritesRunReport()
    {
        var browser = new RecordingBrowserAutomation();
        var options = new BotOptions
        {
            Storage = new StorageOptions
            {
                ScreenshotsDirectory = Path.Combine(_tempDirectory, "screenshots"),
                ArtifactsDirectory = Path.Combine(_tempDirectory, "artifacts"),
            },
        };
        var executor = new ScenarioExecutor(
            options,
            new ScenarioValidationService(),
            [new ThrowingOpenUrlStepHandler()],
            browser);

        var result = await executor.ExecuteAsync(
            new ScenarioDefinition
            {
                Name = "failure-diagnostics",
                Steps =
                [
                    new ScenarioStep
                    {
                        Type = StepType.OpenUrl,
                        Url = "https://example.com",
                    },
                ],
            });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Expected failure.");
        result.RunId.Should().NotBeNullOrWhiteSpace();
        result.FailureScreenshotPath.Should().Be(browser.ScreenshotPath);
        result.FailureHtmlPath.Should().Be(browser.HtmlPath);
        File.Exists(result.FailureHtmlPath).Should().BeTrue();
        File.Exists(result.ReportPath).Should().BeTrue();

        var report = await File.ReadAllTextAsync(result.ReportPath!);
        report.Should().Contain(result.RunId!);
        report.Should().Contain("Expected failure.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenReportCannotBeWritten_PreservesOriginalFailure()
    {
        var artifactRoot = Path.Combine(_tempDirectory, "blocked-artifacts");
        var browser = new RecordingBrowserAutomation
        {
            BlockArtifactDirectoryAfterHtmlCapture = true,
        };
        var executor = new ScenarioExecutor(
            new BotOptions
            {
                Storage = new StorageOptions
                {
                    ScreenshotsDirectory = Path.Combine(_tempDirectory, "screenshots"),
                    ArtifactsDirectory = artifactRoot,
                },
            },
            new ScenarioValidationService(),
            [new ThrowingOpenUrlStepHandler()],
            browser);

        var result = await executor.ExecuteAsync(
            new ScenarioDefinition
            {
                Name = "report-write-failure",
                Steps =
                [
                    new ScenarioStep
                    {
                        Type = StepType.OpenUrl,
                        Url = "https://example.com",
                    },
                ],
            });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Expected failure.");
        result.ReportPath.Should().BeNull();
    }

    private sealed class ThrowingOpenUrlStepHandler : IStepHandler
    {
        public StepType StepType => StepType.OpenUrl;

        public Task<StepExecutionResult> ExecuteAsync(
            ScenarioStep step,
            ScenarioExecutionContext context,
            int index,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Expected failure.");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private sealed class OutputStepHandler : IStepHandler
    {
        private readonly string _value;

        public OutputStepHandler(string value)
        {
            _value = value;
        }

        public StepType StepType => StepType.ReadText;

        public Task<StepExecutionResult> ExecuteAsync(
            ScenarioStep step,
            ScenarioExecutionContext context,
            int index,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                new StepExecutionResult
                {
                    Index = index,
                    Type = step.Type,
                    Success = true,
                    OutputName = step.Output,
                    OutputValue = _value,
                });
    }

    private sealed class RecordingBrowserAutomation : IBrowserAutomation
    {
        public string ReadTextValue { get; init; } = string.Empty;

        public string? NavigatedUrl { get; private set; }

        public string? ScreenshotPath { get; private set; }

        public string? HtmlPath { get; private set; }

        public bool BlockArtifactDirectoryAfterHtmlCapture { get; init; }

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
            CancellationToken cancellationToken = default)
        {
            ScreenshotPath = filePath;
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, "fake screenshot");
            return Task.FromResult(filePath);
        }

        public Task<string> SaveHtmlSnapshotAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            HtmlPath = filePath;
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, "<html><body>failure</body></html>");

            if (BlockArtifactDirectoryAfterHtmlCapture)
            {
                var directory = Path.GetDirectoryName(filePath)!;
                Directory.Delete(directory, recursive: true);
                File.WriteAllText(directory, "block directory recreation");
            }

            return Task.FromResult(filePath);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
