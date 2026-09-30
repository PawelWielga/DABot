using DesktopAutomationBot.Application;
using Microsoft.Extensions.Logging;

namespace DesktopAutomationBot.Infrastructure;

public sealed class LoggingInteractiveBrowserSessionAuditSink(
    ILogger<LoggingInteractiveBrowserSessionAuditSink> logger) :
    IInteractiveBrowserSessionAuditSink
{
    public Task WriteAsync(
        InteractiveBrowserSessionAuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        cancellationToken.ThrowIfCancellationRequested();

        if (auditEvent.EventType == InteractiveBrowserSessionAuditEventType.Started)
        {
            logger.LogInformation(
                "Interactive browser session {SessionId} started for profile {ProfileName} at {OccurredAt}.",
                auditEvent.SessionId,
                auditEvent.ProfileName,
                auditEvent.OccurredAt);
        }
        else
        {
            logger.LogInformation(
                "Interactive browser session {SessionId} ended for profile {ProfileName} at {OccurredAt} with reason {EndReason}.",
                auditEvent.SessionId,
                auditEvent.ProfileName,
                auditEvent.OccurredAt,
                auditEvent.EndReason);
        }

        return Task.CompletedTask;
    }
}
