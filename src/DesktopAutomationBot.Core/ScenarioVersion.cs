using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopAutomationBot.Core;

public sealed class ScenarioVersion
{
    private ScenarioVersion(
        Guid scenarioId,
        Guid versionId,
        int versionNumber,
        int schemaVersion,
        string definitionHash,
        string definitionJson,
        DateTimeOffset createdAt)
    {
        ScenarioId = scenarioId;
        VersionId = versionId;
        VersionNumber = versionNumber;
        SchemaVersion = schemaVersion;
        DefinitionHash = definitionHash;
        DefinitionJson = definitionJson;
        CreatedAt = createdAt;
    }

    public Guid ScenarioId { get; }

    public Guid VersionId { get; }

    public int VersionNumber { get; }

    public int SchemaVersion { get; }

    public string DefinitionHash { get; }

    public string DefinitionJson { get; }

    public DateTimeOffset CreatedAt { get; }

    public static ScenarioVersion Capture(
        Guid scenarioId,
        int versionNumber,
        ScenarioDefinition definition,
        DateTimeOffset createdAt)
    {
        if (scenarioId == Guid.Empty)
        {
            throw new ArgumentException("Scenario ID must not be empty.", nameof(scenarioId));
        }

        if (versionNumber < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(versionNumber),
                versionNumber,
                "Scenario version number must be at least 1.");
        }

        ArgumentNullException.ThrowIfNull(definition);

        var normalizedDefinition = ScenarioDefinitionNormalizer.Normalize(definition);
        var definitionJson = ScenarioDefinitionCanonicalJson.Serialize(normalizedDefinition);
        var definitionHash = ScenarioDefinitionCanonicalJson.ComputeSha256(definitionJson);

        return new ScenarioVersion(
            scenarioId,
            Guid.NewGuid(),
            versionNumber,
            normalizedDefinition.SchemaVersion,
            definitionHash,
            definitionJson,
            createdAt);
    }

    public static ScenarioVersion Restore(
        Guid scenarioId,
        Guid versionId,
        int versionNumber,
        int schemaVersion,
        string definitionHash,
        string definitionJson,
        DateTimeOffset createdAt)
    {
        if (scenarioId == Guid.Empty)
        {
            throw new ArgumentException("Scenario ID must not be empty.", nameof(scenarioId));
        }

        if (versionId == Guid.Empty)
        {
            throw new ArgumentException("Scenario version ID must not be empty.", nameof(versionId));
        }

        if (versionNumber < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(versionNumber),
                versionNumber,
                "Scenario version number must be at least 1.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(definitionHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionJson);

        var definition = ScenarioDefinitionCanonicalJson.Deserialize(definitionJson);
        if (definition.SchemaVersion != schemaVersion)
        {
            throw new ArgumentException(
                $"Stored scenario schema version '{schemaVersion}' does not match definition schema version '{definition.SchemaVersion}'.",
                nameof(schemaVersion));
        }

        var normalizedDefinition = ScenarioDefinitionNormalizer.Normalize(definition);
        var canonicalJson = ScenarioDefinitionCanonicalJson.Serialize(normalizedDefinition);
        if (!string.Equals(canonicalJson, definitionJson, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Stored scenario definition JSON is not the canonical normalized representation.",
                nameof(definitionJson));
        }

        var computedHash = ScenarioDefinitionCanonicalJson.ComputeSha256(definitionJson);
        if (!string.Equals(computedHash, definitionHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Stored scenario definition hash does not match the definition JSON.",
                nameof(definitionHash));
        }

        return new ScenarioVersion(
            scenarioId,
            versionId,
            versionNumber,
            schemaVersion,
            computedHash,
            definitionJson,
            createdAt);
    }

    public ScenarioDefinition MaterializeDefinition() =>
        ScenarioDefinitionCanonicalJson.Deserialize(DefinitionJson);
}

internal static class ScenarioDefinitionCanonicalJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(ScenarioDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var element = JsonSerializer.SerializeToElement(definition, SerializerOptions);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonicalElement(writer, element);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static ScenarioDefinition Deserialize(string definitionJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionJson);

        var definition = JsonSerializer.Deserialize<ScenarioDefinition>(
            definitionJson,
            SerializerOptions);

        return definition
            ?? throw new InvalidOperationException("Stored scenario version JSON could not be deserialized.");
    }

    public static string ComputeSha256(string canonicalJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalJson);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void WriteCanonicalElement(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element
                    .EnumerateObject()
                    .OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalElement(writer, property.Value);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonicalElement(writer, item);
                }

                writer.WriteEndArray();
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }
}
