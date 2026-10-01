using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public interface IScenarioManagementService
{
    Task<ScenarioDocument?> GetAsync(
        string fileName,
        CancellationToken cancellationToken = default);

    Task<ScenarioWriteResult> SaveAsync(
        string fileName,
        string json,
        bool overwrite,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string fileName,
        CancellationToken cancellationToken = default);
}

public sealed record ScenarioDocument(
    string FileName,
    string Json);

public sealed record ScenarioJsonValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors);

public sealed record ScenarioJsonDefinitionResult(
    ScenarioDefinition? Definition,
    IReadOnlyList<string> Errors)
{
    public bool IsValid => Definition is not null && Errors.Count == 0;
}

public sealed record ScenarioWriteResult(
    bool Success,
    IReadOnlyList<string> Errors);

public static class ScenarioFileName
{
    public static void Validate(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException(
                "Scenario file name is required.",
                nameof(fileName));
        }

        if (!string.Equals(
                Path.GetFileName(fileName),
                fileName,
                StringComparison.Ordinal) ||
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            !fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Scenario file name must be a simple .json file name without directory segments.",
                nameof(fileName));
        }
    }
}

public static class ScenarioJsonValidation
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly ScenarioDefinitionValidator Validator = new();

    public static ScenarioJsonValidationResult Validate(string? json)
    {
        var definition = Parse(json);
        return new ScenarioJsonValidationResult(
            definition.IsValid,
            definition.Errors);
    }

    public static ScenarioJsonDefinitionResult Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ScenarioJsonDefinitionResult(
                null,
                ["Scenario JSON is required."]);
        }

        try
        {
            var scenario = JsonSerializer.Deserialize<ScenarioDefinition>(
                json,
                SerializerOptions);

            if (scenario is null)
            {
                return new ScenarioJsonDefinitionResult(
                    null,
                    ["Scenario JSON could not be parsed."]);
            }

            scenario = ScenarioDefinitionNormalizer.Normalize(scenario);
            var validation = Validator.Validate(scenario);

            return new ScenarioJsonDefinitionResult(
                validation.IsValid ? scenario : null,
                validation.Errors);
        }
        catch (JsonException exception)
        {
            var location =
                exception.LineNumber is not null
                    ? $" at line {exception.LineNumber + 1}, byte {exception.BytePositionInLine + 1}"
                    : string.Empty;

            return new ScenarioJsonDefinitionResult(
                null,
                [$"Invalid JSON{location}: {exception.Message}"]);
        }
    }
}
