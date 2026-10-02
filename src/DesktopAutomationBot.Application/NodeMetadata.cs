namespace DesktopAutomationBot.Application;

public sealed record NodeMetadata
{
    public required string DisplayName { get; init; }

    public required string OperatingSystem { get; init; }

    public required string DABotVersion { get; init; }

    public IReadOnlyDictionary<string, string> BrowserVersions { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyList<string> Capabilities { get; init; } = [];

    public required int ExecutionSlots { get; init; }

    public static NodeMetadata Validate(NodeMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadata.DisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadata.OperatingSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadata.DABotVersion);

        if (metadata.ExecutionSlots <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(metadata),
                metadata.ExecutionSlots,
                "Execution slots must be greater than zero.");
        }

        return new NodeMetadata
        {
            DisplayName = metadata.DisplayName.Trim(),
            OperatingSystem = metadata.OperatingSystem.Trim(),
            DABotVersion = metadata.DABotVersion.Trim(),
            BrowserVersions = NormalizeVersions(
                metadata.BrowserVersions),
            Tags = NormalizeLabels(metadata.Tags),
            Capabilities = NormalizeLabels(
                metadata.Capabilities),
            ExecutionSlots = metadata.ExecutionSlots,
        };
    }

    private static IReadOnlyDictionary<string, string> NormalizeVersions(
        IReadOnlyDictionary<string, string>? versions)
    {
        if (versions is null || versions.Count == 0)
        {
            return new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
        }

        var normalized =
            new SortedDictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var pair in versions)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) ||
                string.IsNullOrWhiteSpace(pair.Value))
            {
                continue;
            }

            normalized[pair.Key.Trim()] = pair.Value.Trim();
        }

        return normalized;
    }

    private static IReadOnlyList<string> NormalizeLabels(
        IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return [];
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

public interface INodeMetadataProvider
{
    Task<NodeMetadata> GetAsync(
        CancellationToken cancellationToken = default);
}
