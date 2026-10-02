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
    TimeProvider timeProvider) : IAdministrativeEventService
{
    public async Task<AdministrativeEventPublishResult> PublishAsync(
        AdministrativeEventPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.EventId == Guid.Empty)
        {
            return Failure("Event ID must not be empty.");
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
            return Failure("Payload must be valid JSON.");
        }
        catch (ArgumentException exception)
        {
            return Failure(exception.Message);
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
            return Failure(exception.Message);
        }

        var acceptance = await eventPublisher.PublishAsync(
            automationEvent,
            cancellationToken);

        return new AdministrativeEventPublishResult(
            Success: true,
            Errors: [],
            EventId: automationEvent.EventId,
            IsDuplicate: acceptance.IsDuplicate,
            MatchedWaitingRun: acceptance.MatchedWaitingRun,
            RunId: acceptance.RunId,
            ResumeWorkItemId: acceptance.ResumeWorkItemId);
    }

    private static AdministrativeEventPublishResult Failure(
        string error) =>
        new(
            Success: false,
            Errors: [error]);
}
