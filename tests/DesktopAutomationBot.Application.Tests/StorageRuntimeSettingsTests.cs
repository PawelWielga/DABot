using DesktopAutomationBot.Application;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class StorageRuntimeSettingsTests
{
    [Fact]
    public void FromOptions_MapsStorageOptions()
    {
        var options = new BotOptions
        {
            Storage = new StorageOptions
            {
                ScenariosDirectory = "custom/scenarios",
                ScreenshotsDirectory = "custom/screens",
                ArtifactsDirectory = "custom/artifacts",
                BrowserProfilesDirectory = "custom/profiles",
                DatabasePath = "custom/state.db",
            },
        };

        var settings = StorageRuntimeSettings.FromOptions(options);

        settings.ScenariosDirectory.Should().Be("custom/scenarios");
        settings.ScreenshotsDirectory.Should().Be("custom/screens");
        settings.ArtifactsDirectory.Should().Be("custom/artifacts");
        settings.BrowserProfilesDirectory.Should().Be("custom/profiles");
        settings.DatabasePath.Should().Be("custom/state.db");
    }

    [Fact]
    public void Validate_DefaultSettings_AreValid()
    {
        StorageRuntimeSettingsValidator
            .Validate(new StorageRuntimeSettings())
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void Validate_EmptyPaths_ReturnsErrors()
    {
        var settings = new StorageRuntimeSettings
        {
            ScenariosDirectory = "",
            ScreenshotsDirectory = " ",
            ArtifactsDirectory = "",
            BrowserProfilesDirectory = " ",
            DatabasePath = "",
        };

        var errors = StorageRuntimeSettingsValidator.Validate(settings);

        errors.Should().HaveCount(5);
        errors.Should().Contain(error => error.Contains("Scenarios directory"));
        errors.Should().Contain(error => error.Contains("Screenshots directory"));
        errors.Should().Contain(error => error.Contains("Artifacts directory"));
        errors.Should().Contain(error => error.Contains("Browser profiles directory"));
        errors.Should().Contain(error => error.Contains("Database path"));
    }

    [Fact]
    public void Validate_DuplicateDirectories_ReturnsCollisionError()
    {
        var settings = new StorageRuntimeSettings
        {
            ScenariosDirectory = "shared-data",
            ScreenshotsDirectory = "shared-data",
        };

        var errors = StorageRuntimeSettingsValidator.Validate(settings);

        errors.Should().Contain(
            error =>
                error.Contains("Scenarios directory") &&
                error.Contains("Screenshots directory") &&
                error.Contains("different locations"));
    }

    [Fact]
    public void Validate_FilesystemRootDirectory_IsRejected()
    {
        var root = Path.GetPathRoot(Path.GetFullPath("."))!;
        var settings = new StorageRuntimeSettings
        {
            ScenariosDirectory = root,
        };

        var errors = StorageRuntimeSettingsValidator.Validate(settings);

        errors.Should().Contain(
            error =>
                error.Contains("Scenarios directory") &&
                error.Contains("filesystem root"));
    }

    [Fact]
    public void Validate_DirectoryPointingAtExistingFile_IsRejected()
    {
        var filePath = Path.GetTempFileName();

        try
        {
            var settings = new StorageRuntimeSettings
            {
                ScenariosDirectory = filePath,
            };

            var errors = StorageRuntimeSettingsValidator.Validate(settings);

            errors.Should().Contain(
                error =>
                    error.Contains("Scenarios directory") &&
                    error.Contains("existing file"));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void Validate_DatabasePointingAtExistingDirectory_IsRejected()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"dabot-storage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var settings = new StorageRuntimeSettings
            {
                DatabasePath = directory,
            };

            var errors = StorageRuntimeSettingsValidator.Validate(settings);

            errors.Should().Contain(
                error =>
                    error.Contains("Database path") &&
                    error.Contains("existing directory"));
        }
        finally
        {
            Directory.Delete(directory);
        }
    }

    [Fact]
    public void CloneAndHasSameValues_TrackEditsWithoutSharingState()
    {
        var settings = new StorageRuntimeSettings();
        var clone = settings.Clone();

        settings.HasSameValues(clone).Should().BeTrue();

        clone.DatabasePath = "data/other.db";

        settings.HasSameValues(clone).Should().BeFalse();
        settings.DatabasePath.Should().Be("data/dabot.db");
    }
}
