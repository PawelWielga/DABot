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
    public async Task Queries_WhenDatabaseHasNoRuntimeSchema_ReturnEmptyReadModel()
    {
        var query = new SqliteRunQueryService(
            CreateOptions());

        var summary =
            await query.GetDashboardSummaryAsync();
        var runs =
            await query.ListRecentRunsAsync();

        summary.TotalRuns.Should().Be(0);
        runs.Should().BeEmpty();
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
