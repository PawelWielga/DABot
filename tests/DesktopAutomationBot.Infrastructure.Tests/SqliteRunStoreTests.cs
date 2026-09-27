using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using DesktopAutomationBot.Infrastructure;
using FluentAssertions;

namespace DesktopAutomationBot.Infrastructure.Tests;

public sealed class SqliteRunStoreTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), "dabot-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAndLoad_AcrossStoreInstances_PreservesDurableRun()
    {
        var databasePath = Path.Combine(_tempDirectory, "runs.db");
        var options = CreateOptions(databasePath);
        var scenarioVersion = CreateScenarioVersion();
        var runId = Guid.NewGuid();
        var createdAt = DateTimeOffset.Parse("2026-09-27T12:00:00+02:00");
        var updatedAt = createdAt.AddMinutes(7);
        var cursor = ExecutionCursor.Start(
            scenarioVersion.MaterializeDefinition());

        var run = AutomationRun.Restore(
            runId,
            scenarioVersion,
            RunState.Restore(RunStatus.Waiting, RunWaitReason.Event),
            cursor,
            new Dictionary<string, string>
            {
                ["Correlation"] = "abc-123",
            },
            createdAt,
            updatedAt);

        var firstProcessStore = new SqliteRunStore(options);
        await firstProcessStore.SaveAsync(run, scenarioVersion);

        var restartedProcessStore = new SqliteRunStore(options);
        var loaded = await restartedProcessStore.LoadAsync(runId);

        loaded.Should().NotBeNull();
        loaded!.ScenarioVersion.VersionId.Should().Be(scenarioVersion.VersionId);
        loaded.ScenarioVersion.ScenarioId.Should().Be(scenarioVersion.ScenarioId);
        loaded.ScenarioVersion.DefinitionHash.Should().Be(scenarioVersion.DefinitionHash);

        loaded.Run.RunId.Should().Be(runId);
        loaded.Run.ScenarioVersionId.Should().Be(scenarioVersion.VersionId);
        loaded.Run.State.Status.Should().Be(RunStatus.Waiting);
        loaded.Run.State.WaitReason.Should().Be(RunWaitReason.Event);
        loaded.Run.Cursor.Should().BeEquivalentTo(cursor);
        loaded.Run.Variables["correlation"].Should().Be("abc-123");
        loaded.Run.CreatedAt.Should().Be(createdAt);
        loaded.Run.UpdatedAt.Should().Be(updatedAt);

        File.Exists(databasePath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_WhenRunChanges_UpdatesMutableSnapshot()
    {
        var databasePath = Path.Combine(_tempDirectory, "updates.db");
        var store = new SqliteRunStore(CreateOptions(databasePath));
        var version = CreateScenarioVersion();
        var runId = Guid.NewGuid();
        var createdAt = DateTimeOffset.Parse("2026-09-27T12:00:00+02:00");
        var cursor = ExecutionCursor.Start(version.MaterializeDefinition());

        var queued = AutomationRun.Create(
            version,
            createdAt,
            runId,
            cursor,
            new Dictionary<string, string>
            {
                ["state"] = "queued",
            });

        await store.SaveAsync(queued, version);

        var running = AutomationRun.Restore(
            runId,
            version,
            RunState.Restore(RunStatus.Running),
            cursor,
            new Dictionary<string, string>
            {
                ["state"] = "running",
            },
            createdAt,
            createdAt.AddMinutes(1));

        await store.SaveAsync(running, version);

        var loaded = await store.LoadAsync(runId);

        loaded.Should().NotBeNull();
        loaded!.Run.State.Status.Should().Be(RunStatus.Running);
        loaded.Run.Variables["state"].Should().Be("running");
        loaded.Run.CreatedAt.Should().Be(createdAt);
        loaded.Run.UpdatedAt.Should().Be(createdAt.AddMinutes(1));
    }

    [Fact]
    public async Task SaveAsync_WhenScenarioVersionDoesNotMatchRun_Throws()
    {
        var store = new SqliteRunStore(
            CreateOptions(Path.Combine(_tempDirectory, "mismatch.db")));
        var version = CreateScenarioVersion();
        var differentVersion = CreateScenarioVersion();
        var run = AutomationRun.Create(
            version,
            DateTimeOffset.UtcNow);

        var act = () => store.SaveAsync(run, differentVersion);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*identity must match*");
    }

    [Fact]
    public async Task SaveAsync_WhenSameVersionIdentityHasDifferentDefinition_Throws()
    {
        var databasePath = Path.Combine(_tempDirectory, "immutable-version.db");
        var store = new SqliteRunStore(CreateOptions(databasePath));
        var original = CreateScenarioVersion();
        var runId = Guid.NewGuid();
        var createdAt = DateTimeOffset.Parse("2026-09-27T12:00:00+02:00");
        var originalRun = AutomationRun.Create(
            original,
            createdAt,
            runId);

        await store.SaveAsync(originalRun, original);

        var alteredCaptured = ScenarioVersion.Capture(
            original.ScenarioId,
            original.VersionNumber,
            new ScenarioDefinition
            {
                Name = "Changed",
                Steps =
                [
                    new ScenarioStep
                    {
                        Id = "other",
                        Type = StepType.Screenshot,
                    },
                ],
            },
            original.CreatedAt);

        var altered = ScenarioVersion.Restore(
            original.ScenarioId,
            original.VersionId,
            original.VersionNumber,
            alteredCaptured.SchemaVersion,
            alteredCaptured.DefinitionHash,
            alteredCaptured.DefinitionJson,
            original.CreatedAt);

        var alteredRun = AutomationRun.Restore(
            runId,
            altered,
            RunState.Restore(RunStatus.Running),
            ExecutionCursor.Start(altered.MaterializeDefinition()),
            variables: null,
            createdAt,
            createdAt.AddMinutes(1));

        var act = () => store.SaveAsync(alteredRun, altered);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*different immutable data*");
    }

    [Fact]
    public async Task LoadAsync_WhenRunDoesNotExist_ReturnsNull()
    {
        var store = new SqliteRunStore(
            CreateOptions(Path.Combine(_tempDirectory, "missing.db")));

        var loaded = await store.LoadAsync(Guid.NewGuid());

        loaded.Should().BeNull();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static BotOptions CreateOptions(string databasePath) =>
        new()
        {
            Storage = new StorageOptions
            {
                DatabasePath = databasePath,
            },
        };

    private static ScenarioVersion CreateScenarioVersion() =>
        ScenarioVersion.Capture(
            Guid.NewGuid(),
            1,
            new ScenarioDefinition
            {
                Name = "Persisted scenario",
                Steps =
                [
                    new ScenarioStep
                    {
                        Id = "open",
                        Type = StepType.OpenUrl,
                        Url = "https://example.com",
                    },
                    new ScenarioStep
                    {
                        Id = "capture",
                        Type = StepType.Screenshot,
                    },
                ],
            },
            DateTimeOffset.Parse("2026-09-27T11:00:00+02:00"));
}
