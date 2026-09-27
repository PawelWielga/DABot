using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Infrastructure;

public sealed class JsonScenarioLoader : IScenarioLoader
{
    private readonly BotOptions _options;
    private readonly ScenarioDefinitionValidator _validator = new();
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public JsonScenarioLoader(BotOptions options)
    {
        _options = options;
    }

    public async Task<ScenarioDefinition> LoadAsync(string scenarioPath, CancellationToken cancellationToken = default)
    {
        var resolvedPath = ResolvePath(scenarioPath);
        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException($"Scenario file '{resolvedPath}' was not found.", resolvedPath);
        }

        var json = await File.ReadAllTextAsync(resolvedPath, cancellationToken);
        var scenario = JsonSerializer.Deserialize<ScenarioDefinition>(json, _serializerOptions);
        if (scenario is null)
        {
            throw new ScenarioLoadException(resolvedPath, ["Scenario JSON could not be parsed."]);
        }

        scenario = ScenarioDefinitionNormalizer.Normalize(scenario);

        var validation = _validator.Validate(scenario);
        if (!validation.IsValid)
        {
            throw new ScenarioLoadException(resolvedPath, validation.Errors);
        }

        return scenario;
    }

    private string ResolvePath(string scenarioPath)
    {
        if (Path.IsPathRooted(scenarioPath))
        {
            return Path.GetFullPath(scenarioPath);
        }

        if (File.Exists(scenarioPath))
        {
            return Path.GetFullPath(scenarioPath);
        }

        var scenariosDirectory = string.IsNullOrWhiteSpace(_options.Storage.ScenariosDirectory)
            ? Path.Combine("scenarios")
            : _options.Storage.ScenariosDirectory;

        var candidate = Path.Combine(scenariosDirectory, scenarioPath);
        if (File.Exists(candidate))
        {
            return Path.GetFullPath(candidate);
        }

        return Path.GetFullPath(scenarioPath);
    }
}
