namespace DesktopAutomationBot.Application;

public enum InteractiveBrowserSessionAuditEventType
{
    Started = 1,
    Ended = 2,
}

public enum InteractiveBrowserSessionEndReason
{
    Manual = 1,
    BrowserClosed = 2,
    Expired = 3,
    HostShutdown = 4,
}

public sealed record InteractiveBrowserSessionAuditEvent
{
    public required InteractiveBrowserSessionAuditEventType EventType { get; init; }

    public required Guid SessionId { get; init; }

    public required string ProfileName { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public InteractiveBrowserSessionEndReason? EndReason { get; init; }
}

public interface IInteractiveBrowserSessionAuditSink
{
    Task WriteAsync(
        InteractiveBrowserSessionAuditEvent auditEvent,
        CancellationToken cancellationToken = default);
}
