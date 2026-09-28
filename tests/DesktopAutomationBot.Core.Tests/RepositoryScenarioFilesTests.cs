using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Core.Tests;

public sealed class RepositoryScenarioFilesTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Repository_scenarios_and_examples_should_match_runtime_validation()
    {
        var repositoryRoot = FindRepositoryRoot();
        var files = new[]
            {
                Path.Combine(repositoryRoot, "scenarios"),
                Path.Combine(repositoryRoot, "examples"),
            }
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        files.Should().NotBeEmpty();

        var validator = new ScenarioDefinitionValidator();
        var failures = new List<string>();

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var scenario = JsonSerializer.Deserialize<ScenarioDefinition>(json, SerializerOptions);

                if (scenario is null)
                {
                    failures.Add($"{Path.GetRelativePath(repositoryRoot, file)}: deserialized to null.");
                    continue;
                }

                var validation = validator.Validate(scenario);
                if (!validation.IsValid)
                {
                    failures.Add(
                        $"{Path.GetRelativePath(repositoryRoot, file)}: {string.Join("; ", validation.Errors)}");
                }
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException)
            {
                failures.Add($"{Path.GetRelativePath(repositoryRoot, file)}: {exception.Message}");
            }
        }

        failures.Should().BeEmpty(
            "repository scenarios and examples must stay compatible with runtime parsing and validation");
    }

    [Fact]
    public async Task Scenario_schema_should_be_valid_json_and_list_every_current_step_type()
    {
        var repositoryRoot = FindRepositoryRoot();
        var schemaPath = Path.Combine(repositoryRoot, "schemas", "scenario.schema.json");
        var json = await File.ReadAllTextAsync(schemaPath);

        using var document = JsonDocument.Parse(json);

        var typeOptions = document.RootElement
            .GetProperty("$defs")
            .GetProperty("step")
            .GetProperty("properties")
            .GetProperty("type")
            .GetProperty("oneOf")
            .EnumerateArray()
            .Select(item => item.GetProperty("const").GetString())
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();

        typeOptions.Should().BeEquivalentTo(Enum.GetNames<StepType>());
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DesktopAutomationBot.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the DABot repository root.");
    }
}
