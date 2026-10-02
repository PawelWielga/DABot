using System.Text.Json;
using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public interface IAdministrativeEventService
{
    Task<AdministrativeEventPublishResult> PublishAsync(
        AdministrativeEventPublishRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record AdministrativeEventPublishRequest
{
    public required Guid EventId { get; init; }

    public required string Type { get; init; }

    public required string CorrelationId { get; init; }

    public string? PayloadJson { get; init; }
}

public sealed record AdministrativeEventPublishResult(
    bool Success,
    IReadOnlyList<string> Errors,
    Guid? EventId = null,
    bool IsDuplicate = false,
    bool MatchedWaitingRun = false,
    Guid? RunId = null,
    Guid? ResumeWorkItemId = null);

public sealed class AdministrativeEventService(
    IEventPublisher eventPublisher,
    IAdministrativeAuditSink auditSink,
    TimeProvider timeProvider) : IAdministrativeEventService
{
    public async Task<AdministrativeEventPublishResult> PublishAsync(
        AdministrativeEventPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var operationId = Guid.NewGuid();
        Guid? auditEventId =
            request.EventId == Guid.Empty
                ? null
                : request.EventId;

        await auditSink.WriteAsync(
            CreateAuditEvent(
                operationId,
                AdministrativeAuditOutcome.Requested,
                auditEventId),
            cancellationToken);

        if (request.EventId == Guid.Empty)
        {
            return await RejectAsync(
                operationId,
                auditEventId,
                "Event ID must not be empty.");
        }

        ScenarioVariableValue payload;

        try
        {
            payload = string.IsNullOrWhiteSpace(request.PayloadJson)
                ? ScenarioVariableValue.FromNull()
                : ScenarioVariableValue.ParseJson(request.PayloadJson);
        }
        catch (JsonException)
        {
            return await RejectAsync(
                operationId,
                auditEventId,
                "Payload must be valid JSON.");
        }
        catch (ArgumentException exception)
        {
            return await RejectAsync(
                operationId,
                auditEventId,
                exception.Message);
        }

        AutomationEvent automationEvent;

        try
        {
            automationEvent = AutomationEvent.Create(
                request.EventId,
                request.Type,
                request.CorrelationId,
                payload,
                timeProvider.GetUtcNow());
        }
        catch (ArgumentException exception)
        {
            return await RejectAsync(
                operationId,
                auditEventId,
                exception.Message);
        }

        EventAcceptanceResult acceptance;

        try
        {
            acceptance = await eventPublisher.PublishAsync(
                automationEvent,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            await WriteCompletionBestEffortAsync(
                CreateAuditEvent(
                    operationId,
                    AdministrativeAuditOutcome.Cancelled,
                    automationEvent.EventId,
                    failureType: nameof(OperationCanceledException)));
            throw;
        }
        catch (Exception exception)
        {
            await WriteCompletionBestEffortAsync(
                CreateAuditEvent(
                    operationId,
                    AdministrativeAuditOutcome.Failed,
                    automationEvent.EventId,
                    failureType: exception.GetType().Name));
            throw;
        }

        await WriteCompletionBestEffortAsync(
            CreateAuditEvent(
                operationId,
                AdministrativeAuditOutcome.Succeeded,
                automationEvent.EventId,
                runId: acceptance.RunId,
                resumeWorkItemId: acceptance.ResumeWorkItemId,
                isDuplicate: acceptance.IsDuplicate,
                matchedWaitingRun: acceptance.MatchedWaitingRun));

        return new AdministrativeEventPublishResult(
            Success: true,
            Errors: [],
            EventId: automationEvent.EventId,
            IsDuplicate: acceptance.IsDuplicate,
            MatchedWaitingRun: acceptance.MatchedWaitingRun,
            RunId: acceptance.RunId,
            ResumeWorkItemId: acceptance.ResumeWorkItemId);
    }

    private async Task<AdministrativeEventPublishResult> RejectAsync(
        Guid operationId,
        Guid? eventId,
        string error)
    {
        await WriteCompletionBestEffortAsync(
            CreateAuditEvent(
                operationId,
                AdministrativeAuditOutcome.Rejected,
                eventId,
                failureType: "Validation"));

        return new AdministrativeEventPublishResult(
            Success: false,
            Errors: [error]);
    }

    private AdministrativeAuditEvent CreateAuditEvent(
        Guid operationId,
        AdministrativeAuditOutcome outcome,
        Guid? eventId,
        Guid? runId = null,
        Guid? resumeWorkItemId = null,
        bool? isDuplicate = null,
        bool? matchedWaitingRun = null,
        string? failureType = null) =>
        new()
        {
            OperationId = operationId,
            Operation = AdministrativeOperation.PublishEvent,
            Outcome = outcome,
            OccurredAt = timeProvider.GetUtcNow(),
            RunId = runId,
            EventId = eventId,
            ResumeWorkItemId = resumeWorkItemId,
            IsDuplicate = isDuplicate,
            MatchedWaitingRun = matchedWaitingRun,
            FailureType = failureType,
        };

    private async Task WriteCompletionBestEffortAsync(
        AdministrativeAuditEvent auditEvent)
    {
        try
        {
            await auditSink.WriteAsync(
                auditEvent,
                CancellationToken.None);
        }
        catch
        {
            // The request audit record is written before publishing. A
            // completion-audit failure must not change an already completed
            // event acceptance result.
        }
    }
}
