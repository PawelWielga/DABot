using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using DesktopAutomationBot.Infrastructure;
using FluentAssertions;

namespace DesktopAutomationBot.Infrastructure.Tests;

public sealed class SqliteRunQueryServiceTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(
            Path.GetTempPath(),
            "dabot-query-tests",
            Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task DashboardAndRecentRuns_ReflectPersistedRunState()
    {
        var options = CreateOptions();
        var store = new SqliteRunStore(options);
        var query = new SqliteRunQueryService(options);
        var version = CreateScenarioVersion();
        var now = DateTimeOffset.Parse(
            "2026-09-30T11:40:00+02:00");

        var running = Restore(
            AutomationRun.Create(
                version,
                now.AddMinutes(-5)),
            version,
            RunState.Restore(RunStatus.Running),
            now.AddMinutes(-1));

        var waiting = Restore(
            AutomationRun.Create(
                version,
                now.AddMinutes(-4)),
            version,
            RunState.Restore(
                RunStatus.Waiting,
                RunWaitReason.Human),
            now.AddMinutes(-2));

        var failed = Restore(
            AutomationRun.Create(
                version,
                now.AddMinutes(-3)),
            version,
            RunState.Restore(RunStatus.Failed),
            now);

        await store.SaveAsync(running, version);
        await store.SaveAsync(waiting, version);
        await store.SaveAsync(failed, version);

        var summary =
            await query.GetDashboardSummaryAsync();
        var recent =
            await query.ListRecentRunsAsync();
        var failures =
            await query.ListRecentRunsAsync(
                status: RunStatus.Failed);

        summary.TotalRuns.Should().Be(3);
        summary.RunningRuns.Should().Be(1);
        summary.WaitingRuns.Should().Be(1);
        summary.FailedRuns.Should().Be(1);

        recent.Select(item => item.RunId)
            .Should()
            .Equal(
                failed.RunId,
                running.RunId,
                waiting.RunId);

        failures.Should().ContainSingle();
        failures[0].RunId.Should().Be(failed.RunId);
    }

    [Fact]
    public async Task GetRunAsync_ReturnsScenarioVariablesAndStepAttempts()
    {
        var options = CreateOptions();
        var store = new SqliteRunStore(options);
        var query = new SqliteRunQueryService(options);
        var version = CreateScenarioVersion();
        var now = DateTimeOffset.Parse(
            "2026-09-30T12:20:00+02:00");

        var created = AutomationRun.CreateStructured(
            version,
            now,
            variables: new Dictionary<string, ScenarioVariableValue>
            {
                ["customer"] = ScenarioVariableValue.FromString("CUST-42"),
                ["count"] = ScenarioVariableValue.FromNumber(3),
                ["approved"] = ScenarioVariableValue.FromBoolean(true),
            });

        var running = Restore(
            created,
            version,
            RunState.Restore(RunStatus.Running),
            now.AddSeconds(1));

        await store.SaveAsync(running, version);

        var startedAttempt = StepAttempt.Start(
            running.RunId,
            version.MaterializeDefinition().Steps[0],
            attemptNumber: 1,
            startedAt: now.AddSeconds(2));

        await store.SaveStepAttemptAsync(startedAttempt);

        var completedAttempt =
            startedAttempt.MarkCompleted(
                now.AddSeconds(3));

        await store.SaveStepAttemptAsync(completedAttempt);

        var detail = await query.GetRunAsync(running.RunId);

        detail.Should().NotBeNull();
        detail!.ScenarioName.Should().Be("query model");
        detail.ScenarioVersionNumber.Should().Be(1);
        detail.Status.Should().Be(RunStatus.Running);

        detail.Variables.Should().Contain(
            item =>
                item.Name == "customer" &&
                item.Kind == "String" &&
                item.Value == "CUST-42");
        detail.Variables.Should().Contain(
            item =>
                item.Name == "count" &&
                item.Kind == "Number" &&
                item.Value == "3");
        detail.Variables.Should().Contain(
            item =>
                item.Name == "approved" &&
                item.Kind == "True" &&
                item.Value == "true");

        detail.StepAttempts.Should().ContainSingle();
        var attempt = detail.StepAttempts[0];
        attempt.StepId.Should().Be("open");
        attempt.StepType.Should().Be(StepType.OpenUrl);
        attempt.AttemptNumber.Should().Be(1);
        attempt.Status.Should().Be(StepAttemptStatus.Completed);
        attempt.FinishedAt.Should().Be(now.AddSeconds(3));
    }

    [Fact]
    public async Task Queries_WhenDatabaseHasNoRuntimeSchema_ReturnEmptyReadModel()
    {
        var query = new SqliteRunQueryService(
            CreateOptions());

        var summary =
            await query.GetDashboardSummaryAsync();
        var runs =
            await query.ListRecentRunsAsync();
        var detail =
            await query.GetRunAsync(Guid.NewGuid());

        summary.TotalRuns.Should().Be(0);
        runs.Should().BeEmpty();
        detail.Should().BeNull();
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

    private static AutomationRun Restore(
        AutomationRun source,
        ScenarioVersion version,
        RunState state,
        DateTimeOffset updatedAt) =>
        AutomationRun.RestoreStructured(
            source.RunId,
            version,
            state,
            source.Cursor,
            source.Variables,
            source.CreatedAt,
            updatedAt);

    private static ScenarioVersion CreateScenarioVersion() =>
        ScenarioVersion.Capture(
            Guid.NewGuid(),
            1,
            new ScenarioDefinition
            {
                Name = "query model",
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
            DateTimeOffset.Parse(
                "2026-09-30T11:00:00+02:00"));

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
