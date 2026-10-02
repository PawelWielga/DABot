using DesktopAutomationBot.Application;
using Microsoft.Extensions.Logging;

namespace DesktopAutomationBot.Infrastructure;

public sealed class LoggingAdministrativeAuditSink(
    ILogger<LoggingAdministrativeAuditSink> logger) :
    IAdministrativeAuditSink
{
    public Task WriteAsync(
        AdministrativeAuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        cancellationToken.ThrowIfCancellationRequested();

        if (auditEvent.Outcome is AdministrativeAuditOutcome.Failed
            or AdministrativeAuditOutcome.Rejected
            or AdministrativeAuditOutcome.Cancelled)
        {
            logger.LogWarning(
                "Administrative operation {OperationId} {Operation} finished with {Outcome} at {OccurredAt}. RunId={RunId}; EventId={EventId}; ResumeWorkItemId={ResumeWorkItemId}; IsDuplicate={IsDuplicate}; MatchedWaitingRun={MatchedWaitingRun}; FailureType={FailureType}.",
                auditEvent.OperationId,
                auditEvent.Operation,
                auditEvent.Outcome,
                auditEvent.OccurredAt,
                auditEvent.RunId,
                auditEvent.EventId,
                auditEvent.ResumeWorkItemId,
                auditEvent.IsDuplicate,
                auditEvent.MatchedWaitingRun,
                auditEvent.FailureType);
        }
        else
        {
            logger.LogInformation(
                "Administrative operation {OperationId} {Operation} finished with {Outcome} at {OccurredAt}. RunId={RunId}; EventId={EventId}; ResumeWorkItemId={ResumeWorkItemId}; IsDuplicate={IsDuplicate}; MatchedWaitingRun={MatchedWaitingRun}; FailureType={FailureType}.",
                auditEvent.OperationId,
                auditEvent.Operation,
                auditEvent.Outcome,
                auditEvent.OccurredAt,
                auditEvent.RunId,
                auditEvent.EventId,
                auditEvent.ResumeWorkItemId,
                auditEvent.IsDuplicate,
                auditEvent.MatchedWaitingRun,
                auditEvent.FailureType);
        }

        return Task.CompletedTask;
    }
}
