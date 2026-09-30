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

public sealed record ScenarioWriteResult(
    bool Success,
    IReadOnlyList<string> Errors);

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
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ScenarioJsonValidationResult(
                false,
                ["Scenario JSON is required."]);
        }

        try
        {
            var scenario = JsonSerializer.Deserialize<ScenarioDefinition>(
                json,
                SerializerOptions);

            if (scenario is null)
            {
                return new ScenarioJsonValidationResult(
                    false,
                    ["Scenario JSON could not be parsed."]);
            }

            scenario = ScenarioDefinitionNormalizer.Normalize(scenario);
            var validation = Validator.Validate(scenario);

            return new ScenarioJsonValidationResult(
                validation.IsValid,
                validation.Errors);
        }
        catch (JsonException exception)
        {
            var location =
                exception.LineNumber is not null
                    ? $" at line {exception.LineNumber + 1}, byte {exception.BytePositionInLine + 1}"
                    : string.Empty;

            return new ScenarioJsonValidationResult(
                false,
                [$"Invalid JSON{location}: {exception.Message}"]);
        }
    }
}
