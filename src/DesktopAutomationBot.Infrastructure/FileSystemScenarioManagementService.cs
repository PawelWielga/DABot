using DesktopAutomationBot.Application;

namespace DesktopAutomationBot.Infrastructure;

public sealed class FileSystemScenarioManagementService : IScenarioManagementService
{
    private readonly string _scenariosDirectory;

    public FileSystemScenarioManagementService(BotOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var configuredDirectory = string.IsNullOrWhiteSpace(options.Storage.ScenariosDirectory)
            ? Path.Combine("scenarios")
            : options.Storage.ScenariosDirectory;

        _scenariosDirectory = Path.GetFullPath(configuredDirectory);
    }

    public async Task<ScenarioDocument?> GetAsync(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var path = ResolveFilePath(fileName);
        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return new ScenarioDocument(fileName, json);
    }

    public async Task<ScenarioWriteResult> SaveAsync(
        string fileName,
        string json,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        var validation = ScenarioJsonValidation.Validate(json);
        if (!validation.IsValid)
        {
            return new ScenarioWriteResult(false, validation.Errors);
        }

        var path = ResolveFilePath(fileName);
        Directory.CreateDirectory(_scenariosDirectory);

        if (!overwrite && File.Exists(path))
        {
            return new ScenarioWriteResult(
                false,
                [$"Scenario file '{fileName}' already exists."]);
        }

        var tempPath = Path.Combine(
            _scenariosDirectory,
            $".{fileName}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllTextAsync(tempPath, json, cancellationToken);

            if (overwrite)
            {
                File.Move(tempPath, path, overwrite: true);
            }
            else
            {
                File.Move(tempPath, path);
            }

            return new ScenarioWriteResult(true, []);
        }
        catch (IOException) when (!overwrite && File.Exists(path))
        {
            return new ScenarioWriteResult(
                false,
                [$"Scenario file '{fileName}' already exists."]);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public Task<bool> DeleteAsync(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = ResolveFilePath(fileName);
        if (!File.Exists(path))
        {
            return Task.FromResult(false);
        }

        File.Delete(path);
        return Task.FromResult(true);
    }

    private string ResolveFilePath(string fileName)
    {
        ScenarioFileName.Validate(fileName);

        var path = Path.GetFullPath(
            Path.Combine(_scenariosDirectory, fileName));

        var prefix = _scenariosDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? _scenariosDirectory
            : _scenariosDirectory + Path.DirectorySeparatorChar;

        if (!path.StartsWith(
                prefix,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Scenario path must stay inside the configured scenarios directory.",
                nameof(fileName));
        }

        return path;
    }
}
