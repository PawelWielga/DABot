namespace DesktopAutomationBot.Core;

public enum PageObserverConditionKind
{
    SelectorVisible,
    SelectorHidden,
    TextEquals,
    TextContains,
    TextChanged,
    UrlMatches,
}

public sealed record PageObserverDefinition
{
    public required Guid ObserverId { get; init; }

    public required string Name { get; init; }

    public required string Url { get; init; }

    public string? BrowserProfile { get; init; }

    public required PageObserverConditionKind Condition { get; init; }

    public ScenarioLocator? Locator { get; init; }

    public string? ExpectedValue { get; init; }

    public required string EventType { get; init; }

    public required string CorrelationId { get; init; }

    public int PollIntervalMs { get; init; } = 5000;

    public bool Enabled { get; init; } = true;

    public static PageObserverDefinition Validate(
        PageObserverDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.ObserverId == Guid.Empty)
        {
            throw new ArgumentException(
                "Observer ID must not be empty.",
                nameof(definition));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Url);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.EventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.CorrelationId);

        if (!Uri.TryCreate(
                definition.Url,
                UriKind.Absolute,
                out var uri) ||
            uri.Scheme is not "http" and not "https")
        {
            throw new ArgumentException(
                "Observer URL must be an absolute HTTP or HTTPS URL.",
                nameof(definition));
        }

        if (!string.IsNullOrWhiteSpace(definition.BrowserProfile) &&
            !IsSafeProfileName(definition.BrowserProfile))
        {
            throw new ArgumentException(
                "Observer browser profile must start with an alphanumeric character and contain only alphanumeric characters, '.', '_' or '-' (maximum 64 characters).",
                nameof(definition));
        }

        if (definition.PollIntervalMs <= 0)
        {
            throw new ArgumentException(
                "Observer poll interval must be greater than zero.",
                nameof(definition));
        }

        var needsLocator =
            definition.Condition is
                PageObserverConditionKind.SelectorVisible or
                PageObserverConditionKind.SelectorHidden or
                PageObserverConditionKind.TextEquals or
                PageObserverConditionKind.TextContains or
                PageObserverConditionKind.TextChanged;

        if (needsLocator)
        {
            ArgumentNullException.ThrowIfNull(definition.Locator);
            ArgumentException.ThrowIfNullOrWhiteSpace(
                definition.Locator.Value);
        }

        var needsExpectedValue =
            definition.Condition is
                PageObserverConditionKind.TextEquals or
                PageObserverConditionKind.TextContains or
                PageObserverConditionKind.UrlMatches;

        if (needsExpectedValue)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                definition.ExpectedValue);
        }

        return definition;
    }

    private static bool IsSafeProfileName(string value)
    {
        if (value.Length is < 1 or > 64 ||
            !char.IsLetterOrDigit(value[0]))
        {
            return false;
        }

        return value.All(character =>
            char.IsLetterOrDigit(character) ||
            character is '.' or '_' or '-');
    }
}

public sealed record PageObserverSnapshot
{
    public required Guid ObserverId { get; init; }

    public string? LastObservation { get; init; }

    public bool? LastMatched { get; init; }

    public DateTimeOffset? LastCheckedAt { get; init; }

    public DateTimeOffset? NextCheckAt { get; init; }

    public DateTimeOffset? LastEventAt { get; init; }

    public int FailureCount { get; init; }

    public string? LastError { get; init; }
}

public sealed record StoredPageObserver
{
    public required PageObserverDefinition Definition { get; init; }

    public required PageObserverSnapshot Snapshot { get; init; }
}
