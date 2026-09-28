using System.Text.Json;
using DesktopAutomationBot.Application;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class RepositoryConfigurationSchemaTests
{
    [Fact]
    public async Task Configuration_schema_should_match_current_option_properties()
    {
        var repositoryRoot = FindRepositoryRoot();
        var schemaPath = Path.Combine(repositoryRoot, "schemas", "config.schema.json");
        var json = await File.ReadAllTextAsync(schemaPath);

        using var document = JsonDocument.Parse(json);

        var botProperties = document.RootElement
            .GetProperty("properties")
            .GetProperty("bot")
            .GetProperty("properties");

        PropertyNames(botProperties)
            .Should().BeEquivalentTo(OptionPropertyNames<BotOptions>());

        PropertyNames(botProperties.GetProperty("browser").GetProperty("properties"))
            .Should().BeEquivalentTo(OptionPropertyNames<BrowserOptions>());

        PropertyNames(botProperties.GetProperty("storage").GetProperty("properties"))
            .Should().BeEquivalentTo(OptionPropertyNames<StorageOptions>());

        PropertyNames(botProperties.GetProperty("retryWorker").GetProperty("properties"))
            .Should().BeEquivalentTo(OptionPropertyNames<RetryWorkerOptions>());
    }

    private static string[] PropertyNames(JsonElement properties) =>
        properties.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    private static string[] OptionPropertyNames<T>() =>
        typeof(T).GetProperties()
            .Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

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
