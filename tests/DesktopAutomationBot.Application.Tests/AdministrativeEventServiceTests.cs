using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class AdministrativeEventServiceTests
{
    [Fact]
    public async Task PublishAsync_WhenRequestIsValid_PublishesAndAuditsMetadata()
    {
        var eventId = Guid.Parse(
            "91000000-0000-4000-8000-000000000001");
        var runId = Guid.Parse(
            "92000000-0000-4000-8000-000000000001");
        var workItemId = Guid.Parse(
            "93000000-0000-4000-8000-000000000001");
        var now = new DateTimeOffset(
            2026,
            10,
            2,
            6,
            30,
            0,
            TimeSpan.Zero);
        var publisher = new RecordingPublisher(
            new EventAcceptanceResult
            {
                IsDuplicate = false,
                MatchedWaitingRun = true,
                RunId = runId,
                ResumeWorkItemId = workItemId,
            });
        var audit = new RecordingAdministrativeAuditSink();
        var service = new AdministrativeEventService(
            publisher,
            audit,
            new FixedTimeProvider(now));

        var result = await service.PublishAsync(
            new AdministrativeEventPublishRequest
            {
                EventId = eventId,
                Type = "approval.completed",
                CorrelationId = "approval-42",
                PayloadJson = """{"approved":true,"count":2}""",
            });

        result.Success.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.EventId.Should().Be(eventId);
        result.IsDuplicate.Should().BeFalse();
        result.MatchedWaitingRun.Should().BeTrue();
        result.RunId.Should().Be(runId);
        result.ResumeWorkItemId.Should().Be(workItemId);

        publisher.PublishedEvent.Should().NotBeNull();
        publisher.PublishedEvent!.EventId.Should().Be(eventId);
        publisher.PublishedEvent.Type.Should().Be("approval.completed");
        publisher.PublishedEvent.CorrelationId.Should().Be("approval-42");
        publisher.PublishedEvent.OccurredAt.Should().Be(now);
        publisher.PublishedEvent.Payload
            .ToJsonElement()
            .GetProperty("approved")
            .GetBoolean()
            .Should()
            .BeTrue();

        audit.Events.Should().HaveCount(2);
        audit.Events[0].Operation.Should()
            .Be(AdministrativeOperation.PublishEvent);
        audit.Events[0].Outcome.Should()
            .Be(AdministrativeAuditOutcome.Requested);
        audit.Events[0].EventId.Should().Be(eventId);

        audit.Events[1].OperationId.Should()
            .Be(audit.Events[0].OperationId);
        audit.Events[1].Outcome.Should()
            .Be(AdministrativeAuditOutcome.Succeeded);
        audit.Events[1].EventId.Should().Be(eventId);
        audit.Events[1].RunId.Should().Be(runId);
        audit.Events[1].ResumeWorkItemId.Should().Be(workItemId);
        audit.Events[1].IsDuplicate.Should().BeFalse();
        audit.Events[1].MatchedWaitingRun.Should().BeTrue();
    }

    [Fact]
    public async Task PublishAsync_WhenPayloadIsInvalid_AuditsRejectedWithoutPublishing()
    {
        var eventId = Guid.NewGuid();
        var publisher = new RecordingPublisher(
            new EventAcceptanceResult
            {
                IsDuplicate = false,
                MatchedWaitingRun = false,
            });
        var audit = new RecordingAdministrativeAuditSink();
        var service = new AdministrativeEventService(
            publisher,
            audit,
            new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = await service.PublishAsync(
            new AdministrativeEventPublishRequest
            {
                EventId = eventId,
                Type = "approval.completed",
                CorrelationId = "approval-42",
                PayloadJson = "{not-json",
            });

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(
            "Payload must be valid JSON.");
        publisher.PublishedEvent.Should().BeNull();

        audit.Events.Select(item => item.Outcome)
            .Should()
            .Equal(
                AdministrativeAuditOutcome.Requested,
                AdministrativeAuditOutcome.Rejected);
        audit.Events[1].EventId.Should().Be(eventId);
        audit.Events[1].FailureType.Should().Be("Validation");
    }

    [Fact]
    public async Task PublishAsync_WhenEventMetadataIsInvalid_AuditsRejected()
    {
        var publisher = new RecordingPublisher(
            new EventAcceptanceResult
            {
                IsDuplicate = false,
                MatchedWaitingRun = false,
            });
        var audit = new RecordingAdministrativeAuditSink();
        var service = new AdministrativeEventService(
            publisher,
            audit,
            new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = await service.PublishAsync(
            new AdministrativeEventPublishRequest
            {
                EventId = Guid.NewGuid(),
                Type = " ",
                CorrelationId = "approval-42",
                PayloadJson = "null",
            });

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        publisher.PublishedEvent.Should().BeNull();
        audit.Events.Last().Outcome.Should()
            .Be(AdministrativeAuditOutcome.Rejected);
    }

    [Fact]
    public void AdministrativeAuditEvent_DoesNotExposeEventContentFields()
    {
        var properties = typeof(AdministrativeAuditEvent)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        properties.Should().NotContain("Payload");
        properties.Should().NotContain("PayloadJson");
        properties.Should().NotContain("CorrelationId");
        properties.Should().NotContain("EventType");
    }

    private sealed class RecordingPublisher(
        EventAcceptanceResult result) : IEventPublisher
    {
        public AutomationEvent? PublishedEvent { get; private set; }

        public Task<EventAcceptanceResult> PublishAsync(
            AutomationEvent automationEvent,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PublishedEvent = automationEvent;
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingAdministrativeAuditSink :
        IAdministrativeAuditSink
    {
        public List<AdministrativeAuditEvent> Events { get; } = [];

        public Task WriteAsync(
            AdministrativeAuditEvent auditEvent,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(
        DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
