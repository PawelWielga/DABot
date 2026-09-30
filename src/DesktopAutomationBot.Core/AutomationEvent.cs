namespace DesktopAutomationBot.Core;

public sealed record AutomationEvent
{
    public required Guid EventId { get; init; }

    public required string Type { get; init; }

    public required string CorrelationId { get; init; }

    public required ScenarioVariableValue Payload { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public static AutomationEvent Create(
        Guid eventId,
        string type,
        string correlationId,
        ScenarioVariableValue? payload,
        DateTimeOffset occurredAt)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException(
                "Event ID must not be empty.",
                nameof(eventId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        return new AutomationEvent
        {
            EventId = eventId,
            Type = type.Trim(),
            CorrelationId = correlationId.Trim(),
            Payload = payload ?? ScenarioVariableValue.FromNull(),
            OccurredAt = occurredAt,
        };
    }
}

public sealed record EventWaitRegistration
{
    public required Guid RunId { get; init; }

    public required string CorrelationId { get; init; }

    public string? EventType { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

public enum ResumeWorkItemStatus
{
    Pending,
    Completed,
    Failed,
}

public sealed record ResumeWorkItem
{
    public required Guid WorkItemId { get; init; }

    public required Guid RunId { get; init; }

    public required Guid EventId { get; init; }

    public required ResumeWorkItemStatus Status { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }

    public string? ErrorMessage { get; init; }
}
