using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public enum AdministrativeOperation
{
    ResumeRun = 1,
    CancelRun = 2,
    PublishEvent = 3,
}

public enum AdministrativeAuditOutcome
{
    Requested = 1,
    Succeeded = 2,
    Rejected = 3,
    Failed = 4,
    Cancelled = 5,
}

public sealed record AdministrativeAuditEvent
{
    public required Guid OperationId { get; init; }

    public required AdministrativeOperation Operation { get; init; }

    public required AdministrativeAuditOutcome Outcome { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public Guid? RunId { get; init; }

    public Guid? EventId { get; init; }

    public Guid? ResumeWorkItemId { get; init; }

    public bool? IsDuplicate { get; init; }

    public bool? MatchedWaitingRun { get; init; }

    public string? FailureType { get; init; }
}

public interface IAdministrativeAuditSink
{
    Task WriteAsync(
        AdministrativeAuditEvent auditEvent,
        CancellationToken cancellationToken = default);
}

public interface IAdministrativeRunControlService
{
    Task<DurableScenarioExecutionResult> ResumeAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    Task<AutomationRun> CancelAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
}

public sealed class AdministrativeRunControlService(
    IDurableRunControlService runControl,
    IAdministrativeAuditSink auditSink,
    TimeProvider timeProvider) : IAdministrativeRunControlService
{
    public async Task<DurableScenarioExecutionResult> ResumeAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var operationId = Guid.NewGuid();

        await WriteRequestedAsync(
            operationId,
            AdministrativeOperation.ResumeRun,
            runId,
            cancellationToken);

        try
        {
            var result = await runControl.ResumeManuallyAsync(
                runId,
                cancellationToken);

            await WriteCompletionBestEffortAsync(
                operationId,
                AdministrativeOperation.ResumeRun,
                AdministrativeAuditOutcome.Succeeded,
                runId);

            return result;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            await WriteCompletionBestEffortAsync(
                operationId,
                AdministrativeOperation.ResumeRun,
                AdministrativeAuditOutcome.Cancelled,
                runId,
                failureType: nameof(OperationCanceledException));
            throw;
        }
        catch (Exception exception)
        {
            await WriteCompletionBestEffortAsync(
                operationId,
                AdministrativeOperation.ResumeRun,
                AdministrativeAuditOutcome.Failed,
                runId,
                failureType: exception.GetType().Name);
            throw;
        }
    }

    public async Task<AutomationRun> CancelAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var operationId = Guid.NewGuid();

        await WriteRequestedAsync(
            operationId,
            AdministrativeOperation.CancelRun,
            runId,
            cancellationToken);

        try
        {
            var result = await runControl.CancelAsync(
                runId,
                cancellationToken);

            await WriteCompletionBestEffortAsync(
                operationId,
                AdministrativeOperation.CancelRun,
                AdministrativeAuditOutcome.Succeeded,
                runId);

            return result;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            await WriteCompletionBestEffortAsync(
                operationId,
                AdministrativeOperation.CancelRun,
                AdministrativeAuditOutcome.Cancelled,
                runId,
                failureType: nameof(OperationCanceledException));
            throw;
        }
        catch (Exception exception)
        {
            await WriteCompletionBestEffortAsync(
                operationId,
                AdministrativeOperation.CancelRun,
                AdministrativeAuditOutcome.Failed,
                runId,
                failureType: exception.GetType().Name);
            throw;
        }
    }

    private Task WriteRequestedAsync(
        Guid operationId,
        AdministrativeOperation operation,
        Guid runId,
        CancellationToken cancellationToken) =>
        auditSink.WriteAsync(
            new AdministrativeAuditEvent
            {
                OperationId = operationId,
                Operation = operation,
                Outcome = AdministrativeAuditOutcome.Requested,
                OccurredAt = timeProvider.GetUtcNow(),
                RunId = runId,
            },
            cancellationToken);

    private async Task WriteCompletionBestEffortAsync(
        Guid operationId,
        AdministrativeOperation operation,
        AdministrativeAuditOutcome outcome,
        Guid runId,
        string? failureType = null)
    {
        try
        {
            await auditSink.WriteAsync(
                new AdministrativeAuditEvent
                {
                    OperationId = operationId,
                    Operation = operation,
                    Outcome = outcome,
                    OccurredAt = timeProvider.GetUtcNow(),
                    RunId = runId,
                    FailureType = failureType,
                },
                CancellationToken.None);
        }
        catch
        {
            // The requested audit record is written before the state-changing
            // operation. A completion-audit failure must not make an already
            // completed run operation appear to have failed to the caller.
        }
    }
}
