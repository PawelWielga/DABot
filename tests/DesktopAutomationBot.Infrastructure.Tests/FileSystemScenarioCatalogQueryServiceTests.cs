using DesktopAutomationBot.Application;
using DesktopAutomationBot.Infrastructure;
using FluentAssertions;

namespace DesktopAutomationBot.Infrastructure.Tests;

public sealed class FileSystemScenarioCatalogQueryServiceTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(
            Path.GetTempPath(),
            "dabot-scenario-catalog-tests",
            Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ListAsync_ReturnsValidAndInvalidScenarioFiles()
    {
        Directory.CreateDirectory(_tempDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(_tempDirectory, "valid.json"),
            """
            {
              "schemaVersion": 1,
              "name": "Catalog scenario",
              "browserProfile": "work",
              "steps": [
                {
                  "id": "open",
                  "type": "OpenUrl",
                  "url": "https://example.test"
                }
              ]
            }
            """);

        await File.WriteAllTextAsync(
            Path.Combine(_tempDirectory, "invalid.json"),
            """
            {
              "schemaVersion": 1,
              "name": "Broken scenario",
              "steps": [
                {
                  "id": "missing-url",
                  "type": "OpenUrl"
                }
              ]
            }
            """);

        var options = new BotOptions
        {
            Storage = new StorageOptions
            {
                ScenariosDirectory = _tempDirectory,
            },
        };
        var loader = new JsonScenarioLoader(options);
        var query = new FileSystemScenarioCatalogQueryService(
            options,
            loader);

        var scenarios = await query.ListAsync();

        scenarios.Should().HaveCount(2);

        var invalid = scenarios.Single(item => item.FileName == "invalid.json");
        invalid.IsValid.Should().BeFalse();
        invalid.ValidationErrors.Should().NotBeEmpty();

        var valid = scenarios.Single(item => item.FileName == "valid.json");
        valid.IsValid.Should().BeTrue();
        valid.Name.Should().Be("Catalog scenario");
        valid.SchemaVersion.Should().Be(1);
        valid.StepCount.Should().Be(1);
        valid.BrowserProfile.Should().Be("work");
    }

    [Fact]
    public async Task ListAsync_WhenDirectoryDoesNotExist_ReturnsEmptyList()
    {
        var options = new BotOptions
        {
            Storage = new StorageOptions
            {
                ScenariosDirectory = Path.Combine(
                    _tempDirectory,
                    "missing"),
            },
        };
        var query = new FileSystemScenarioCatalogQueryService(
            options,
            new JsonScenarioLoader(options));

        var scenarios = await query.ListAsync();

        scenarios.Should().BeEmpty();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(
                _tempDirectory,
                recursive: true);
        }
    }
}
