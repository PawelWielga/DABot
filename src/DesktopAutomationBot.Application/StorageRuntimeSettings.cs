namespace DesktopAutomationBot.Application;

public interface IStorageRuntimeSettingsService
{
    Task<StorageRuntimeSettings> GetAsync(
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        StorageRuntimeSettings settings,
        CancellationToken cancellationToken = default);
}

public sealed class StorageRuntimeSettings
{
    public string ScenariosDirectory { get; set; } = "scenarios";

    public string ScreenshotsDirectory { get; set; } = "screenshots";

    public string ArtifactsDirectory { get; set; } = "artifacts";

    public string BrowserProfilesDirectory { get; set; } =
        Path.Combine("data", "browser-profiles");

    public string DatabasePath { get; set; } =
        Path.Combine("data", "dabot.db");

    public static StorageRuntimeSettings FromOptions(BotOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new StorageRuntimeSettings
        {
            ScenariosDirectory = options.Storage.ScenariosDirectory,
            ScreenshotsDirectory = options.Storage.ScreenshotsDirectory,
            ArtifactsDirectory = options.Storage.ArtifactsDirectory,
            BrowserProfilesDirectory = options.Storage.BrowserProfilesDirectory,
            DatabasePath = options.Storage.DatabasePath,
        };
    }

    public StorageRuntimeSettings Clone() =>
        new()
        {
            ScenariosDirectory = ScenariosDirectory,
            ScreenshotsDirectory = ScreenshotsDirectory,
            ArtifactsDirectory = ArtifactsDirectory,
            BrowserProfilesDirectory = BrowserProfilesDirectory,
            DatabasePath = DatabasePath,
        };

    public bool HasSameValues(StorageRuntimeSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return SamePathText(ScenariosDirectory, other.ScenariosDirectory) &&
               SamePathText(ScreenshotsDirectory, other.ScreenshotsDirectory) &&
               SamePathText(ArtifactsDirectory, other.ArtifactsDirectory) &&
               SamePathText(BrowserProfilesDirectory, other.BrowserProfilesDirectory) &&
               SamePathText(DatabasePath, other.DatabasePath);
    }

    private static bool SamePathText(
        string left,
        string right) =>
        string.Equals(
            left?.Trim(),
            right?.Trim(),
            StringComparison.Ordinal);
}

public static class StorageRuntimeSettingsValidator
{
    public static IReadOnlyList<string> Validate(
        StorageRuntimeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var errors = new List<string>();

        var scenarios = ValidateDirectory(
            settings.ScenariosDirectory,
            "Scenarios directory",
            errors);
        var screenshots = ValidateDirectory(
            settings.ScreenshotsDirectory,
            "Screenshots directory",
            errors);
        var artifacts = ValidateDirectory(
            settings.ArtifactsDirectory,
            "Artifacts directory",
            errors);
        var profiles = ValidateDirectory(
            settings.BrowserProfilesDirectory,
            "Browser profiles directory",
            errors);
        var database = ValidateDatabasePath(
            settings.DatabasePath,
            errors);

        var directories = new[]
        {
            ("Scenarios directory", scenarios),
            ("Screenshots directory", screenshots),
            ("Artifacts directory", artifacts),
            ("Browser profiles directory", profiles),
        };

        for (var left = 0; left < directories.Length; left++)
        {
            if (directories[left].Item2 is null)
            {
                continue;
            }

            for (var right = left + 1; right < directories.Length; right++)
            {
                if (directories[right].Item2 is null)
                {
                    continue;
                }

                if (PathsEqual(
                        directories[left].Item2!,
                        directories[right].Item2!))
                {
                    errors.Add(
                        $"{directories[left].Item1} and {directories[right].Item1} must resolve to different locations.");
                }
            }
        }

        if (database is not null)
        {
            foreach (var directory in directories)
            {
                if (directory.Item2 is not null &&
                    PathsEqual(database, directory.Item2))
                {
                    errors.Add(
                        $"Database path must not resolve to the same path as {directory.Item1.ToLowerInvariant()}.");
                }
            }
        }

        return errors;
    }

    public static void ValidateOrThrow(
        StorageRuntimeSettings settings)
    {
        var errors = Validate(settings);

        if (errors.Count > 0)
        {
            throw new ArgumentException(
                string.Join(Environment.NewLine, errors),
                nameof(settings));
        }
    }

    private static string? ValidateDirectory(
        string value,
        string label,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{label} is required.");
            return null;
        }

        string fullPath;

        try
        {
            fullPath = Path.GetFullPath(value.Trim());
        }
        catch (Exception exception)
            when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            errors.Add($"{label} is not a valid path.");
            return null;
        }

        var root = Path.GetPathRoot(fullPath);
        if (!string.IsNullOrWhiteSpace(root) &&
            PathsEqual(fullPath, root))
        {
            errors.Add($"{label} must not be a filesystem root.");
        }

        if (File.Exists(fullPath))
        {
            errors.Add($"{label} must identify a directory, but an existing file is present at that path.");
        }

        return fullPath;
    }

    private static string? ValidateDatabasePath(
        string value,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add("Database path is required.");
            return null;
        }

        string fullPath;

        try
        {
            fullPath = Path.GetFullPath(value.Trim());
        }
        catch (Exception exception)
            when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            errors.Add("Database path is not a valid path.");
            return null;
        }

        var root = Path.GetPathRoot(fullPath);
        if (!string.IsNullOrWhiteSpace(root) &&
            PathsEqual(fullPath, root))
        {
            errors.Add("Database path must identify a file, not a filesystem root.");
            return fullPath;
        }

        if (string.IsNullOrWhiteSpace(Path.GetFileName(fullPath)))
        {
            errors.Add("Database path must identify a file.");
        }

        if (Directory.Exists(fullPath))
        {
            errors.Add("Database path must identify a file, but an existing directory is present at that path.");
        }

        return fullPath;
    }

    private static bool PathsEqual(
        string left,
        string right) =>
        string.Equals(
            Path.GetFullPath(left)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
}
