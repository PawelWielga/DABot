using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using DesktopAutomationBot.Infrastructure;
using FluentAssertions;

namespace DesktopAutomationBot.Infrastructure.Tests;

public sealed class SqliteEventHistoryQueryServiceTests :
    IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(
            Path.GetTempPath(),
            "dabot-event-query-tests",
            Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ListRecentAsync_ReturnsMatchedAndUnmatchedEventsWithoutPayload()
    {
        var options = CreateOptions();
        var store = new SqliteRunStore(options);
        var query = new SqliteEventHistoryQueryService(options);
        var version = CreateScenarioVersion();
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-2);

        var created = AutomationRun.Create(
            version,
            createdAt);
        var waiting = AutomationRun.RestoreStructured(
            created.RunId,
            version,
            RunState.Restore(
                RunStatus.Waiting,
                RunWaitReason.Event),
            created.Cursor,
            created.Variables,
            created.CreatedAt,
            createdAt.AddSeconds(1));

        await store.ArmEventWaitAsync(
            waiting,
            version,
            new EventWaitRegistration
            {
                RunId = waiting.RunId,
                CorrelationId = "approval-42",
                EventType = "approval.completed",
                CreatedAt = waiting.UpdatedAt,
            });

        var matchedEvent = AutomationEvent.Create(
            Guid.NewGuid(),
            "approval.completed",
            "approval-42",
            ScenarioVariableValue.ParseJson(
                """{"secret":"do-not-expose"}"""),
            createdAt.AddSeconds(2));

        var matchedAcceptance =
            await store.AcceptAsync(matchedEvent);

        var unmatchedEvent = AutomationEvent.Create(
            Guid.NewGuid(),
            "inventory.changed",
            "inventory-404",
            ScenarioVariableValue.ParseJson(
                """{"stock":7}"""),
            createdAt.AddSeconds(3));

        await store.AcceptAsync(unmatchedEvent);

        var items = await query.ListRecentAsync();

        items.Should().HaveCount(2);

        var matched =
            items.Single(item => item.EventId == matchedEvent.EventId);
        matched.MatchedWaitingRun.Should().BeTrue();
        matched.RunId.Should().Be(waiting.RunId);
        matched.WorkItemId.Should()
            .Be(matchedAcceptance.ResumeWorkItemId);
        matched.WorkItemStatus.Should()
            .Be(ResumeWorkItemStatus.Pending);
        matched.AttemptCount.Should().Be(0);

        var unmatched =
            items.Single(item => item.EventId == unmatchedEvent.EventId);
        unmatched.MatchedWaitingRun.Should().BeFalse();
        unmatched.RunId.Should().BeNull();
        unmatched.WorkItemId.Should().BeNull();
        unmatched.WorkItemStatus.Should().BeNull();

        typeof(EventHistoryItem)
            .GetProperty("Payload")
            .Should()
            .BeNull();
    }

    [Fact]
    public async Task ListRecentAsync_WhenDatabaseHasNoRuntimeSchema_ReturnsEmpty()
    {
        var query =
            new SqliteEventHistoryQueryService(
                CreateOptions());

        var items = await query.ListRecentAsync();

        items.Should().BeEmpty();
    }

    [Fact]
    public async Task ListRecentAsync_RejectsNonPositiveLimit()
    {
        var query =
            new SqliteEventHistoryQueryService(
                CreateOptions());

        var action =
            () => query.ListRecentAsync(limit: 0);

        await action.Should()
            .ThrowAsync<ArgumentOutOfRangeException>();
    }

    private BotOptions CreateOptions() =>
        new()
        {
            Storage = new StorageOptions
            {
                DatabasePath = Path.Combine(
                    _tempDirectory,
                    "runs.db"),
            },
        };

    private static ScenarioVersion CreateScenarioVersion() =>
        ScenarioVersion.Capture(
            Guid.NewGuid(),
            1,
            new ScenarioDefinition
            {
                Name = "event history query",
                Steps =
                [
                    new ScenarioStep
                    {
                        Id = "open",
                        Type = StepType.OpenUrl,
                        Url = "https://example.test",
                    },
                ],
            },
            DateTimeOffset.UtcNow.AddMinutes(-5));

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(
                _tempDirectory,
                recursive: true);
        }
    }
}
