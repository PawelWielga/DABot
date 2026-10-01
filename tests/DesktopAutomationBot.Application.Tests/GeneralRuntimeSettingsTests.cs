using DesktopAutomationBot.Application;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class GeneralRuntimeSettingsTests
{
    [Fact]
    public void FromOptions_MapsCurrentRuntimeOptions()
    {
        var options = new BotOptions
        {
            ScenarioPath = "scenarios/custom.json",
            Browser = new BrowserOptions
            {
                Headless = false,
                SlowMoMs = 25,
                TimeoutMs = 45_000,
                ViewportWidth = 1440,
                ViewportHeight = 900,
            },
            RetryWorker = new RetryWorkerOptions
            {
                PollIntervalMs = 1500,
                BatchSize = 25,
            },
            EventWorker = new EventWorkerOptions
            {
                PollIntervalMs = 2000,
                BatchSize = 30,
                MaxAttempts = 7,
                BaseRetryDelayMs = 500,
                MaxRetryDelayMs = 10_000,
            },
            ObserverWorker = new ObserverWorkerOptions
            {
                PollIntervalMs = 2500,
                BatchSize = 15,
                MaxErrorBackoffMs = 20_000,
            },
            InteractiveBrowser = new InteractiveBrowserOptions
            {
                MaxDurationSeconds = 900,
            },
        };

        var settings = GeneralRuntimeSettings.FromOptions(options);

        settings.ScenarioPath.Should().Be("scenarios/custom.json");
        settings.BrowserHeadless.Should().BeFalse();
        settings.BrowserSlowMoMs.Should().Be(25);
        settings.BrowserTimeoutMs.Should().Be(45_000);
        settings.BrowserViewportWidth.Should().Be(1440);
        settings.BrowserViewportHeight.Should().Be(900);
        settings.RetryWorkerPollIntervalMs.Should().Be(1500);
        settings.RetryWorkerBatchSize.Should().Be(25);
        settings.EventWorkerPollIntervalMs.Should().Be(2000);
        settings.EventWorkerBatchSize.Should().Be(30);
        settings.EventWorkerMaxAttempts.Should().Be(7);
        settings.EventWorkerBaseRetryDelayMs.Should().Be(500);
        settings.EventWorkerMaxRetryDelayMs.Should().Be(10_000);
        settings.ObserverWorkerPollIntervalMs.Should().Be(2500);
        settings.ObserverWorkerBatchSize.Should().Be(15);
        settings.ObserverWorkerMaxErrorBackoffMs.Should().Be(20_000);
        settings.InteractiveBrowserMaxDurationSeconds.Should().Be(900);
    }

    [Fact]
    public void Validate_DefaultSettings_AreValid()
    {
        GeneralRuntimeSettingsValidator
            .Validate(new GeneralRuntimeSettings())
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void Validate_InvalidNumericSettings_ReturnsAllRelevantErrors()
    {
        var settings = new GeneralRuntimeSettings
        {
            BrowserSlowMoMs = -1,
            BrowserTimeoutMs = -1,
            BrowserViewportWidth = 0,
            BrowserViewportHeight = 0,
            RetryWorkerPollIntervalMs = 0,
            RetryWorkerBatchSize = 0,
            EventWorkerPollIntervalMs = 0,
            EventWorkerBatchSize = 0,
            EventWorkerMaxAttempts = 0,
            EventWorkerBaseRetryDelayMs = 2000,
            EventWorkerMaxRetryDelayMs = 1000,
            ObserverWorkerPollIntervalMs = 0,
            ObserverWorkerBatchSize = 0,
            ObserverWorkerMaxErrorBackoffMs = 0,
            InteractiveBrowserMaxDurationSeconds = 0,
        };

        var errors = GeneralRuntimeSettingsValidator.Validate(settings);

        errors.Should().Contain(error => error.Contains("slow motion"));
        errors.Should().Contain(error => error.Contains("Browser timeout"));
        errors.Should().Contain(error => error.Contains("viewport"));
        errors.Should().Contain(error => error.Contains("Retry worker poll interval"));
        errors.Should().Contain(error => error.Contains("Event worker maximum attempts"));
        errors.Should().Contain(error => error.Contains("greater than or equal"));
        errors.Should().Contain(error => error.Contains("Observer worker"));
        errors.Should().Contain(error => error.Contains("Interactive browser"));
    }
}
