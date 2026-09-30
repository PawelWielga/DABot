using DesktopAutomationBot.Infrastructure;
using FluentAssertions;

namespace DesktopAutomationBot.Infrastructure.Tests;

public sealed class FileSystemScenarioManagementServiceTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(
            Path.GetTempPath(),
            "dabot-scenario-management-tests",
            Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveGetAndDeleteAsync_ManagesValidScenarioDocument()
    {
        var service = CreateService();
        var json = """
            {
              "schemaVersion": 1,
              "name": "Managed scenario",
              "steps": [
                {
                  "id": "open",
                  "type": "OpenUrl",
                  "url": "https://example.test"
                }
              ]
            }
            """;

        var save = await service.SaveAsync(
            "managed.json",
            json,
            overwrite: false);

        save.Success.Should().BeTrue();

        var document = await service.GetAsync("managed.json");
        document.Should().NotBeNull();
        document!.Json.Should().Be(json);

        var deleted = await service.DeleteAsync("managed.json");
        deleted.Should().BeTrue();
        (await service.GetAsync("managed.json")).Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_InvalidScenario_DoesNotWriteFile()
    {
        var service = CreateService();

        var result = await service.SaveAsync(
            "invalid.json",
            """
            {
              "schemaVersion": 1,
              "name": "Invalid",
              "steps": [
                {
                  "id": "open",
                  "type": "OpenUrl"
                }
              ]
            }
            """,
            overwrite: false);

        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        File.Exists(Path.Combine(_tempDirectory, "invalid.json"))
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task SaveAsync_WhenCreateWouldOverwriteExistingFile_ReturnsConflict()
    {
        var service = CreateService();
        var json = ValidScenarioJson("First");

        (await service.SaveAsync(
            "same.json",
            json,
            overwrite: false)).Success.Should().BeTrue();

        var second = await service.SaveAsync(
            "same.json",
            ValidScenarioJson("Second"),
            overwrite: false);

        second.Success.Should().BeFalse();
        second.Errors.Should().ContainSingle(
            error => error.Contains("already exists", StringComparison.OrdinalIgnoreCase));

        var document = await service.GetAsync("same.json");
        document!.Json.Should().Be(json);
    }

    [Theory]
    [InlineData("../outside.json")]
    [InlineData("nested/scenario.json")]
    [InlineData("scenario.txt")]
    public async Task ManagementOperations_RejectUnsafeFileNames(string fileName)
    {
        var service = CreateService();

        var save = async () => await service.SaveAsync(
            fileName,
            ValidScenarioJson("Unsafe"),
            overwrite: false);

        await save.Should().ThrowAsync<ArgumentException>();
    }

    private FileSystemScenarioManagementService CreateService()
    {
        Directory.CreateDirectory(_tempDirectory);

        return new FileSystemScenarioManagementService(
            new BotOptions
            {
                Storage = new StorageOptions
                {
                    ScenariosDirectory = _tempDirectory,
                },
            });
    }

    private static string ValidScenarioJson(string name) =>
        $$"""
        {
          "schemaVersion": 1,
          "name": "{{name}}",
          "steps": [
            {
              "id": "open",
              "type": "OpenUrl",
              "url": "https://example.test"
            }
          ]
        }
        """;

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
