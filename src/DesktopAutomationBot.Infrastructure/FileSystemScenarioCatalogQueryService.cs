using System.Text.Json;
using DesktopAutomationBot.Application;

namespace DesktopAutomationBot.Infrastructure;

public sealed class FileSystemScenarioCatalogQueryService : IScenarioCatalogQueryService
{
    private readonly BotOptions _options;
    private readonly IScenarioLoader _loader;

    public FileSystemScenarioCatalogQueryService(
        BotOptions options,
        IScenarioLoader loader)
    {
        _options = options;
        _loader = loader;
    }

    public async Task<IReadOnlyList<ScenarioListItem>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var directory = ResolveScenarioDirectory();
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var files = Directory
            .EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var items = new List<ScenarioListItem>(files.Length);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var scenario = await _loader.LoadAsync(file, cancellationToken);
                items.Add(
                    new ScenarioListItem(
                        Path.GetFileName(file),
                        scenario.Name,
                        scenario.SchemaVersion,
                        scenario.Steps.Count,
                        scenario.BrowserProfile,
                        IsValid: true,
                        ValidationErrors: []));
            }
            catch (ScenarioLoadException exception)
            {
                items.Add(CreateInvalidItem(file, exception.Errors));
            }
            catch (JsonException exception)
            {
                items.Add(CreateInvalidItem(file, [exception.Message]));
            }
            catch (IOException exception)
            {
                items.Add(CreateInvalidItem(file, [exception.Message]));
            }
            catch (UnauthorizedAccessException exception)
            {
                items.Add(CreateInvalidItem(file, [exception.Message]));
            }
        }

        return items;
    }

    private string ResolveScenarioDirectory()
    {
        var configuredDirectory = string.IsNullOrWhiteSpace(_options.Storage.ScenariosDirectory)
            ? Path.Combine("scenarios")
            : _options.Storage.ScenariosDirectory;

        return Path.GetFullPath(configuredDirectory);
    }

    private static ScenarioListItem CreateInvalidItem(
        string file,
        IReadOnlyList<string> validationErrors) =>
        new(
            Path.GetFileName(file),
            Path.GetFileNameWithoutExtension(file),
            SchemaVersion: 0,
            StepCount: 0,
            BrowserProfile: null,
            IsValid: false,
            ValidationErrors: validationErrors);
}
