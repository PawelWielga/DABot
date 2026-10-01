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


    [Fact]
    public async Task ExecuteAsync_WithNestedIfAndLoop_ExecutesExpectedChildren()
    {
        var browser = new RecordingBrowserAutomation();
        var handler = new CountingScreenshotStepHandler();
        var executor = new ScenarioExecutor(
            new BotOptions
            {
                Storage = new StorageOptions
                {
                    ScreenshotsDirectory = Path.Combine(_tempDirectory, "screenshots"),
                    ArtifactsDirectory = Path.Combine(_tempDirectory, "artifacts"),
                },
            },
            new ScenarioValidationService(),
            [handler],
            browser);

        var result = await executor.ExecuteAsync(
            new ScenarioDefinition
            {
                Name = "control-flow",
                Steps =
                [
                    new ScenarioStep
                    {
                        Type = StepType.Loop,
                        Value = "3",
                        Children =
                        [
                            new ScenarioStep
                            {
                                Type = StepType.If,
                                Value = "true",
                                Children =
                                [
                                    new ScenarioStep
                                    {
                                        Type = StepType.Screenshot,
                                    },
                                ],
                            },
                        ],
                    },
                    new ScenarioStep
                    {
                        Type = StepType.If,
                        Value = "false",
                        Children =
                        [
                            new ScenarioStep
                            {
                                Type = StepType.Screenshot,
                            },
                        ],
                    },
                ],
            });

        result.Success.Should().BeTrue();
        handler.ExecutionCount.Should().Be(3);
        result.Steps.Should().HaveCount(3);
    }


    [Fact]
    public async Task ExecuteAsync_DisabledStepsAndSubtrees_AreSkipped()
    {
        var browser = new RecordingBrowserAutomation();
        var handler = new CountingScreenshotStepHandler();
        var executor = new ScenarioExecutor(
            new BotOptions
            {
                Storage = new StorageOptions
                {
                    ScreenshotsDirectory = Path.Combine(_tempDirectory, "screenshots"),
                    ArtifactsDirectory = Path.Combine(_tempDirectory, "artifacts"),
                },
            },
            new ScenarioValidationService(),
            [handler],
            browser);

        var result = await executor.ExecuteAsync(
            new ScenarioDefinition
            {
                Name = "disabled-steps",
                Steps =
                [
                    new ScenarioStep
                    {
                        Type = StepType.Screenshot,
                        Enabled = false,
                    },
                    new ScenarioStep
                    {
                        Type = StepType.Loop,
                        Enabled = false,
                        Value = "3",
                        Children =
                        [
                            new ScenarioStep
                            {
                                Type = StepType.Screenshot,
                            },
                        ],
                    },
                    new ScenarioStep
                    {
                        Type = StepType.Screenshot,
                    },
                ],
            });

        result.Success.Should().BeTrue();
        handler.ExecutionCount.Should().Be(1);
        result.Steps.Should().ContainSingle();
    }


    [Fact]
    public async Task ExecuteAsync_WhenStepFailsWithinRetryBudget_RetriesAndSucceeds()
    {
        var browser = new RecordingBrowserAutomation();
        var handler = new FlakyScreenshotStepHandler(failuresBeforeSuccess: 2);
        var executor = new ScenarioExecutor(
            new BotOptions
            {
                Storage = new StorageOptions
                {
                    ScreenshotsDirectory = Path.Combine(_tempDirectory, "screenshots"),
                    ArtifactsDirectory = Path.Combine(_tempDirectory, "artifacts"),
                },
            },
            new ScenarioValidationService(),
            [handler],
            browser);

        var result = await executor.ExecuteAsync(
            new ScenarioDefinition
            {
                Name = "retry-test",
                Steps =
                [
                    new ScenarioStep
                    {
                        Type = StepType.Screenshot,
                        RetryCount = 2,
                        RetryDelayMs = 0,
                    },
                ],
            });

        result.Success.Should().BeTrue();
        handler.ExecutionCount.Should().Be(3);
        result.Steps.Should().ContainSingle();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRetryBudgetIsExhausted_ReturnsFailure()
    {
        var browser = new RecordingBrowserAutomation();
        var handler = new FlakyScreenshotStepHandler(failuresBeforeSuccess: 3);
        var executor = new ScenarioExecutor(
            new BotOptions
            {
                Storage = new StorageOptions
                {
                    ScreenshotsDirectory = Path.Combine(_tempDirectory, "screenshots"),
                    ArtifactsDirectory = Path.Combine(_tempDirectory, "artifacts"),
                },
            },
            new ScenarioValidationService(),
            [handler],
            browser);

        var result = await executor.ExecuteAsync(
            new ScenarioDefinition
            {
                Name = "retry-failure",
                Steps =
                [
                    new ScenarioStep
                    {
                        Type = StepType.Screenshot,
                        RetryCount = 1,
                        RetryDelayMs = 0,
                    },
                ],
            });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Transient failure.");
        handler.ExecutionCount.Should().Be(2);
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



    private sealed class FlakyScreenshotStepHandler : IStepHandler
    {
        private readonly int _failuresBeforeSuccess;

        public FlakyScreenshotStepHandler(int failuresBeforeSuccess)
        {
            _failuresBeforeSuccess = failuresBeforeSuccess;
        }

        public int ExecutionCount { get; private set; }

        public StepType StepType => StepType.Screenshot;

        public Task<StepExecutionResult> ExecuteAsync(
            ScenarioStep step,
            ScenarioExecutionContext context,
            int index,
            CancellationToken cancellationToken)
        {
            ExecutionCount++;

            if (ExecutionCount <= _failuresBeforeSuccess)
            {
                throw new InvalidOperationException("Transient failure.");
            }

            return Task.FromResult(
                new StepExecutionResult
                {
                    Index = index,
                    Type = step.Type,
                    Success = true,
                });
        }
    }

    private sealed class CountingScreenshotStepHandler : IStepHandler
    {
        public int ExecutionCount { get; private set; }

        public StepType StepType => StepType.Screenshot;

        public Task<StepExecutionResult> ExecuteAsync(
            ScenarioStep step,
            ScenarioExecutionContext context,
            int index,
            CancellationToken cancellationToken)
        {
            ExecutionCount++;
            return Task.FromResult(
                new StepExecutionResult
                {
                    Index = index,
                    Type = step.Type,
                    Success = true,
                });
        }
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
