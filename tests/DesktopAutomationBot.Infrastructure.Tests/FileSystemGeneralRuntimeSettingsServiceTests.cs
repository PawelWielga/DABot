using System.Text.Json.Nodes;
using DesktopAutomationBot.Application;
using DesktopAutomationBot.Infrastructure;
using FluentAssertions;

namespace DesktopAutomationBot.Infrastructure.Tests;

public sealed class FileSystemGeneralRuntimeSettingsServiceTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(
            Path.GetTempPath(),
            "dabot-config-tests",
            Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAsync_UpdatesGeneralSettingsAndPreservesUnownedSections()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "appsettings.json");

        await File.WriteAllTextAsync(
            path,
            """
            {
              "bot": {
                "storage": {
                  "databasePath": "data/custom.db"
                },
                "browser": {
                  "headless": true
                },
                "futureSetting": {
                  "preserve": true
                }
              },
              "Logging": {
                "LogLevel": {
                  "Default": "Warning"
                }
              },
              "customRoot": {
                "keep": "yes"
              }
            }
            """);

        var service = new FileSystemGeneralRuntimeSettingsService(path);

        await service.SaveAsync(
            new GeneralRuntimeSettings
            {
                ScenarioPath = "scenarios/edited.json",
                BrowserHeadless = false,
                BrowserSlowMoMs = 10,
                BrowserTimeoutMs = 45_000,
                BrowserViewportWidth = 1600,
                BrowserViewportHeight = 900,
                RetryWorkerPollIntervalMs = 1200,
                RetryWorkerBatchSize = 40,
                EventWorkerPollIntervalMs = 1300,
                EventWorkerBatchSize = 50,
                EventWorkerMaxAttempts = 6,
                EventWorkerBaseRetryDelayMs = 750,
                EventWorkerMaxRetryDelayMs = 15_000,
                ObserverWorkerPollIntervalMs = 1400,
                ObserverWorkerBatchSize = 20,
                ObserverWorkerMaxErrorBackoffMs = 30_000,
                InteractiveBrowserMaxDurationSeconds = 600,
            });

        var root = JsonNode.Parse(
            await File.ReadAllTextAsync(path))!
            .AsObject();

        root["bot"]!["storage"]!["databasePath"]!
            .GetValue<string>()
            .Should()
            .Be("data/custom.db");
        root["bot"]!["futureSetting"]!["preserve"]!
            .GetValue<bool>()
            .Should()
            .BeTrue();
        root["Logging"]!["LogLevel"]!["Default"]!
            .GetValue<string>()
            .Should()
            .Be("Warning");
        root["customRoot"]!["keep"]!
            .GetValue<string>()
            .Should()
            .Be("yes");

        root["bot"]!["scenarioPath"]!
            .GetValue<string>()
            .Should()
            .Be("scenarios/edited.json");
        root["bot"]!["browser"]!["headless"]!
            .GetValue<bool>()
            .Should()
            .BeFalse();
        root["bot"]!["eventWorker"]!["maxAttempts"]!
            .GetValue<int>()
            .Should()
            .Be(6);
        root["bot"]!["interactiveBrowser"]!["maxDurationSeconds"]!
            .GetValue<int>()
            .Should()
            .Be(600);
    }

    [Fact]
    public async Task GetAsync_MergesPersistedValuesWithRuntimeDefaults()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "appsettings.json");

        await File.WriteAllTextAsync(
            path,
            """
            {
              "bot": {
                "browser": {
                  "headless": false,
                  "viewportWidth": 1920
                },
                "retryWorker": {
                  "batchSize": 12
                }
              }
            }
            """);

        var service = new FileSystemGeneralRuntimeSettingsService(path);

        var settings = await service.GetAsync();

        settings.BrowserHeadless.Should().BeFalse();
        settings.BrowserViewportWidth.Should().Be(1920);
        settings.BrowserViewportHeight.Should().Be(720);
        settings.BrowserTimeoutMs.Should().Be(30_000);
        settings.RetryWorkerBatchSize.Should().Be(12);
        settings.RetryWorkerPollIntervalMs.Should().Be(1000);
        settings.EventWorkerMaxAttempts.Should().Be(5);
        settings.InteractiveBrowserMaxDurationSeconds.Should().Be(1800);
    }

    [Fact]
    public async Task SaveAsync_BlankScenarioPath_RemovesPersistedOverride()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "appsettings.json");

        await File.WriteAllTextAsync(
            path,
            """
            {
              "bot": {
                "scenarioPath": "scenarios/old.json"
              }
            }
            """);

        var service = new FileSystemGeneralRuntimeSettingsService(path);
        var settings = await service.GetAsync();
        settings.ScenarioPath = "   ";

        await service.SaveAsync(settings);

        var root = JsonNode.Parse(
            await File.ReadAllTextAsync(path))!
            .AsObject();

        root["bot"]!.AsObject()
            .ContainsKey("scenarioPath")
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task SaveAsync_InvalidSettings_DoesNotModifyFile()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "appsettings.json");
        const string original = """
            {
              "bot": {
                "browser": {
                  "headless": true
                }
              }
            }
            """;
        await File.WriteAllTextAsync(path, original);

        var service = new FileSystemGeneralRuntimeSettingsService(path);

        var action = () => service.SaveAsync(
            new GeneralRuntimeSettings
            {
                BrowserViewportWidth = 0,
            });

        await action.Should().ThrowAsync<ArgumentException>();

        (await File.ReadAllTextAsync(path))
            .Should()
            .Be(original);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
