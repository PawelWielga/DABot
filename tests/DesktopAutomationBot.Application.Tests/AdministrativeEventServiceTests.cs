using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class AdministrativeEventServiceTests
{
    [Fact]
    public async Task PublishAsync_WhenRequestIsValid_PublishesTransportNeutralEvent()
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
        var service = new AdministrativeEventService(
            publisher,
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
    }

    [Fact]
    public async Task PublishAsync_WhenPayloadIsInvalid_ReturnsValidationError()
    {
        var publisher = new RecordingPublisher(
            new EventAcceptanceResult
            {
                IsDuplicate = false,
                MatchedWaitingRun = false,
            });
        var service = new AdministrativeEventService(
            publisher,
            new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = await service.PublishAsync(
            new AdministrativeEventPublishRequest
            {
                EventId = Guid.NewGuid(),
                Type = "approval.completed",
                CorrelationId = "approval-42",
                PayloadJson = "{not-json",
            });

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(
            "Payload must be valid JSON.");
        publisher.PublishedEvent.Should().BeNull();
    }

    [Fact]
    public async Task PublishAsync_WhenEventMetadataIsInvalid_ReturnsValidationError()
    {
        var publisher = new RecordingPublisher(
            new EventAcceptanceResult
            {
                IsDuplicate = false,
                MatchedWaitingRun = false,
            });
        var service = new AdministrativeEventService(
            publisher,
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

    private sealed class FixedTimeProvider(
        DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
