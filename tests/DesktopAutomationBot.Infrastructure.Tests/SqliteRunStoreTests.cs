using System.Text.Json;
using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using DesktopAutomationBot.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;

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
        loaded.Run.Variables["correlation"].ToInterpolationString().Should().Be("abc-123");
        loaded.Run.CreatedAt.Should().Be(createdAt);
        loaded.Run.UpdatedAt.Should().Be(updatedAt);

        File.Exists(databasePath).Should().BeTrue();
    }


    [Fact]
    public async Task SaveAndLoad_PreservesStructuredVariableJsonTypes()
    {
        var databasePath = Path.Combine(
            _tempDirectory,
            "structured-variables.db");
        var options = CreateOptions(databasePath);
        var version = CreateScenarioVersion();
        var run = AutomationRun.CreateStructured(
            version,
            DateTimeOffset.UtcNow,
            variables: new Dictionary<string, ScenarioVariableValue>
            {
                ["enabled"] = ScenarioVariableValue.FromBoolean(true),
                ["count"] = ScenarioVariableValue.FromNumber(12),
                ["payload"] = ScenarioVariableValue.ParseJson(
                    """{"items":[1,2],"name":"stored"}"""),
            });

        var store = new SqliteRunStore(options);
        await store.SaveAsync(run, version);

        var loaded = await new SqliteRunStore(options)
            .LoadAsync(run.RunId);

        loaded.Should().NotBeNull();
        loaded!.Run.Variables["enabled"].Kind
            .Should().Be(JsonValueKind.True);
        loaded.Run.Variables["count"].ToJsonElement()
            .GetInt32().Should().Be(12);
        loaded.Run.Variables["payload"].ToJsonElement()
            .GetProperty("items")
            .GetArrayLength()
            .Should().Be(2);
    }

    [Fact]
    public async Task SaveAndLoad_RetryWait_PreservesRetryNotBefore()
    {
        var databasePath = Path.Combine(_tempDirectory, "retry-schedule.db");
        var options = CreateOptions(databasePath);
        var version = CreateScenarioVersion();
        var createdAt = DateTimeOffset.Parse("2026-09-28T10:00:00+02:00");
        var updatedAt = createdAt.AddMinutes(1);
        var retryNotBefore = updatedAt.AddSeconds(45);
        var queued = AutomationRun.Create(version, createdAt);

        var waiting = AutomationRun.RestoreStructured(
            queued.RunId,
            version,
            RunState.Restore(RunStatus.Waiting, RunWaitReason.Retry),
            queued.Cursor,
            queued.Variables,
            createdAt,
            updatedAt,
            retryNotBefore);

        var firstStore = new SqliteRunStore(options);
        await firstStore.SaveAsync(waiting, version);

        var restartedStore = new SqliteRunStore(options);
        var loaded = await restartedStore.LoadAsync(waiting.RunId);

        loaded.Should().NotBeNull();
        loaded!.Run.State.Status.Should().Be(RunStatus.Waiting);
        loaded.Run.State.WaitReason.Should().Be(RunWaitReason.Retry);
        loaded.Run.RetryNotBefore.Should().Be(retryNotBefore);
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
        loaded.Run.Variables["state"].ToInterpolationString().Should().Be("running");
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

    [Fact]
    public async Task SaveStepAttemptAsync_AcrossStoreInstances_PreservesCompletedAttempt()
    {
        var databasePath = Path.Combine(_tempDirectory, "attempt-lifecycle.db");
        var options = CreateOptions(databasePath);
        var version = CreateScenarioVersion();
        var startedAt = DateTimeOffset.Parse("2026-09-27T12:10:00+02:00");
        var run = AutomationRun.Create(
            version,
            startedAt.AddMinutes(-1));
        var step = version.MaterializeDefinition().Steps[0];
        var started = StepAttempt.Start(run.RunId, step, 1, startedAt);
        var completed = started.MarkCompleted(startedAt.AddSeconds(2));

        var firstProcessStore = new SqliteRunStore(options);
        await firstProcessStore.SaveAsync(run, version);
        await firstProcessStore.SaveStepAttemptAsync(started);
        await firstProcessStore.SaveStepAttemptAsync(completed);

        var restartedProcessStore = new SqliteRunStore(options);
        var attempts = await restartedProcessStore.LoadStepAttemptsAsync(run.RunId);

        attempts.Should().ContainSingle();
        attempts[0].Should().Be(completed);
    }

    [Fact]
    public async Task SaveStepAttemptAsync_WhenFinalizedAttemptWasNotPersistedAsStarted_Throws()
    {
        var databasePath = Path.Combine(_tempDirectory, "attempt-ordering.db");
        var store = new SqliteRunStore(CreateOptions(databasePath));
        var version = CreateScenarioVersion();
        var startedAt = DateTimeOffset.Parse("2026-09-27T12:20:00+02:00");
        var run = AutomationRun.Create(version, startedAt.AddMinutes(-1));
        var step = version.MaterializeDefinition().Steps[0];
        var completed = StepAttempt.Start(run.RunId, step, 1, startedAt)
            .MarkCompleted(startedAt.AddSeconds(1));

        await store.SaveAsync(run, version);

        var act = () => store.SaveStepAttemptAsync(completed);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be persisted as Started*");
    }

    [Fact]
    public async Task SaveStepAttemptAsync_WhenRunWasNotPersisted_Throws()
    {
        var store = new SqliteRunStore(
            CreateOptions(Path.Combine(_tempDirectory, "attempt-parent.db")));
        var version = CreateScenarioVersion();
        var run = AutomationRun.Create(
            version,
            DateTimeOffset.Parse("2026-09-27T12:25:00+02:00"));
        var attempt = StepAttempt.Start(
            run.RunId,
            version.MaterializeDefinition().Steps[0],
            1,
            run.CreatedAt.AddSeconds(1));

        var act = () => store.SaveStepAttemptAsync(attempt);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be persisted before its step attempts*");
    }

    [Fact]
    public async Task SaveStepAttemptAsync_WhenImmutableIdentityChanges_Throws()
    {
        var databasePath = Path.Combine(_tempDirectory, "attempt-identity.db");
        var store = new SqliteRunStore(CreateOptions(databasePath));
        var version = CreateScenarioVersion();
        var startedAt = DateTimeOffset.Parse("2026-09-27T12:30:00+02:00");
        var run = AutomationRun.Create(version, startedAt.AddMinutes(-1));
        var attempt = StepAttempt.Start(
            run.RunId,
            version.MaterializeDefinition().Steps[0],
            1,
            startedAt);

        await store.SaveAsync(run, version);
        await store.SaveStepAttemptAsync(attempt);

        var changedIdentity = attempt with
        {
            StepId = "capture",
        };

        var act = () => store.SaveStepAttemptAsync(changedIdentity);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*different immutable identity data*");
    }

    [Fact]
    public async Task MarkStartedAttemptsUnknownAsync_AfterRestart_OnlyMarksInterruptedAttempts()
    {
        var databasePath = Path.Combine(_tempDirectory, "attempt-recovery.db");
        var options = CreateOptions(databasePath);
        var version = CreateScenarioVersion();
        var startedAt = DateTimeOffset.Parse("2026-09-27T12:40:00+02:00");
        var run = AutomationRun.Create(version, startedAt.AddMinutes(-1));
        var definition = version.MaterializeDefinition();

        var interruptedClick = StepAttempt.Start(
            run.RunId,
            new ScenarioStep
            {
                Id = "submit",
                Type = StepType.Click,
            },
            1,
            startedAt);

        var completedRead = StepAttempt.Start(
            run.RunId,
            definition.Steps[0],
            1,
            startedAt.AddSeconds(1));
        var completedReadFinal = completedRead.MarkCompleted(
            startedAt.AddSeconds(2));

        var firstProcessStore = new SqliteRunStore(options);
        await firstProcessStore.SaveAsync(run, version);
        await firstProcessStore.SaveStepAttemptAsync(interruptedClick);
        await firstProcessStore.SaveStepAttemptAsync(completedRead);
        await firstProcessStore.SaveStepAttemptAsync(completedReadFinal);

        var restartedProcessStore = new SqliteRunStore(options);
        var recovered = await restartedProcessStore.MarkStartedAttemptsUnknownAsync(
            run.RunId,
            startedAt.AddMinutes(1));

        recovered.Should().ContainSingle();
        recovered[0].AttemptId.Should().Be(interruptedClick.AttemptId);
        recovered[0].Status.Should().Be(StepAttemptStatus.Unknown);
        StepAttemptRecoveryPolicy.Decide(recovered[0])
            .Should().Be(StepRecoveryAction.VerifyBeforeRetry);

        var attempts = await restartedProcessStore.LoadStepAttemptsAsync(run.RunId);
        attempts.Should().HaveCount(2);
        attempts.Single(x => x.AttemptId == interruptedClick.AttemptId)
            .Status.Should().Be(StepAttemptStatus.Unknown);
        attempts.Single(x => x.AttemptId == completedRead.AttemptId)
            .Status.Should().Be(StepAttemptStatus.Completed);
    }

    [Fact]
    public async Task ExistingVersion1Database_IsMigratedToVersion3()
    {
        var databasePath = Path.Combine(_tempDirectory, "schema-v1.db");
        await CreateVersion1DatabaseAsync(databasePath);

        var store = new SqliteRunStore(CreateOptions(databasePath));
        var version = CreateScenarioVersion();
        var createdAt = DateTimeOffset.Parse("2026-09-27T13:00:00+02:00");
        var run = AutomationRun.Create(version, createdAt);
        var attempt = StepAttempt.Start(
            run.RunId,
            version.MaterializeDefinition().Steps[0],
            1,
            createdAt.AddSeconds(1));

        await store.SaveAsync(run, version);
        await store.SaveStepAttemptAsync(attempt);

        var attempts = await store.LoadStepAttemptsAsync(run.RunId);
        attempts.Should().ContainSingle();

        await using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
            }.ToString());
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        var rawVersion = await command.ExecuteScalarAsync();

        Convert.ToInt32(rawVersion).Should().Be(3);
    }

    [Fact]
    public async Task ExistingVersion2RetryWait_IsMigratedAndBackfilledToVersion3()
    {
        var databasePath = Path.Combine(_tempDirectory, "schema-v2.db");
        var version = CreateScenarioVersion();
        var runId = Guid.NewGuid();
        var createdAt = DateTimeOffset.Parse("2026-09-28T10:30:00+02:00");
        var updatedAt = createdAt.AddMinutes(2);
        var cursor = ExecutionCursor.Start(version.MaterializeDefinition());

        await CreateVersion2RetryWaitDatabaseAsync(
            databasePath,
            version,
            runId,
            cursor,
            createdAt,
            updatedAt);

        var store = new SqliteRunStore(CreateOptions(databasePath));
        var loaded = await store.LoadAsync(runId);

        loaded.Should().NotBeNull();
        loaded!.Run.State.Status.Should().Be(RunStatus.Waiting);
        loaded.Run.State.WaitReason.Should().Be(RunWaitReason.Retry);
        loaded.Run.RetryNotBefore.Should().Be(updatedAt);

        await using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
            }.ToString());
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        var rawVersion = await command.ExecuteScalarAsync();

        Convert.ToInt32(rawVersion).Should().Be(3);
    }

    [Fact]
    public async Task LoadDueRetryRunIdsAsync_ReturnsOnlyDueRetryWaitsInDueOrderAndHonorsLimit()
    {
        var databasePath = Path.Combine(_tempDirectory, "due-retries.db");
        var store = new SqliteRunStore(CreateOptions(databasePath));
        var version = CreateScenarioVersion();
        var now = DateTimeOffset.Parse("2026-09-28T12:00:00+02:00");
        var createdAt = now.AddHours(-1);

        var oldestDue = CreateRun(
            version,
            RunState.Restore(RunStatus.Waiting, RunWaitReason.Retry),
            createdAt,
            now.AddMinutes(-10),
            now.AddMinutes(-5));

        var newestDue = CreateRun(
            version,
            RunState.Restore(RunStatus.Waiting, RunWaitReason.Retry),
            createdAt.AddMinutes(1),
            now.AddMinutes(-4),
            now.AddMinutes(-1));

        var futureRetry = CreateRun(
            version,
            RunState.Restore(RunStatus.Waiting, RunWaitReason.Retry),
            createdAt.AddMinutes(2),
            now.AddMinutes(-3),
            now.AddMinutes(5));

        var humanWait = CreateRun(
            version,
            RunState.Restore(RunStatus.Waiting, RunWaitReason.Human),
            createdAt.AddMinutes(3),
            now.AddMinutes(-2));

        var running = CreateRun(
            version,
            RunState.Restore(RunStatus.Running),
            createdAt.AddMinutes(4),
            now.AddMinutes(-1));

        foreach (var run in new[]
                 {
                     newestDue,
                     futureRetry,
                     humanWait,
                     running,
                     oldestDue,
                 })
        {
            await store.SaveAsync(run, version);
        }

        var allDue = await store.LoadDueRetryRunIdsAsync(now, limit: 10);
        var firstDue = await store.LoadDueRetryRunIdsAsync(now, limit: 1);

        allDue.Should().Equal(oldestDue.RunId, newestDue.RunId);
        firstDue.Should().Equal(oldestDue.RunId);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static AutomationRun CreateRun(
        ScenarioVersion version,
        RunState state,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? retryNotBefore = null)
    {
        var queued = AutomationRun.Create(
            version,
            createdAt,
            Guid.NewGuid());

        return AutomationRun.RestoreStructured(
            queued.RunId,
            version,
            state,
            queued.Cursor,
            queued.Variables,
            createdAt,
            updatedAt,
            retryNotBefore);
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

    private static async Task CreateVersion2RetryWaitDatabaseAsync(
        string databasePath,
        ScenarioVersion version,
        Guid runId,
        ExecutionCursor cursor,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        await using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
            }.ToString());
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE ScenarioVersions (
                ScenarioId TEXT NOT NULL,
                VersionId TEXT NOT NULL PRIMARY KEY,
                VersionNumber INTEGER NOT NULL,
                SchemaVersion INTEGER NOT NULL,
                DefinitionHash TEXT NOT NULL,
                DefinitionJson TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UNIQUE (ScenarioId, VersionNumber)
            );

            CREATE TABLE Runs (
                RunId TEXT NOT NULL PRIMARY KEY,
                ScenarioId TEXT NOT NULL,
                ScenarioVersionId TEXT NOT NULL,
                Status TEXT NOT NULL,
                WaitReason TEXT NULL,
                CursorJson TEXT NOT NULL,
                VariablesJson TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                FOREIGN KEY (ScenarioVersionId)
                    REFERENCES ScenarioVersions(VersionId)
                    ON DELETE RESTRICT
            );

            CREATE INDEX IX_Runs_ScenarioVersionId
                ON Runs(ScenarioVersionId);

            CREATE TABLE StepAttempts (
                AttemptId TEXT NOT NULL PRIMARY KEY,
                RunId TEXT NOT NULL,
                StepId TEXT NOT NULL,
                StepType TEXT NOT NULL,
                AttemptNumber INTEGER NOT NULL CHECK (AttemptNumber >= 1),
                RetrySafety TEXT NOT NULL,
                Status TEXT NOT NULL,
                StartedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                FinishedAt TEXT NULL,
                ErrorMessage TEXT NULL,
                UNIQUE (RunId, StepId, AttemptNumber),
                FOREIGN KEY (RunId)
                    REFERENCES Runs(RunId)
                    ON DELETE CASCADE
            );

            CREATE INDEX IX_StepAttempts_RunId
                ON StepAttempts(RunId);

            CREATE INDEX IX_StepAttempts_RunId_Status
                ON StepAttempts(RunId, Status);

            INSERT INTO ScenarioVersions (
                ScenarioId,
                VersionId,
                VersionNumber,
                SchemaVersion,
                DefinitionHash,
                DefinitionJson,
                CreatedAt)
            VALUES (
                $scenarioId,
                $versionId,
                $versionNumber,
                $schemaVersion,
                $definitionHash,
                $definitionJson,
                $scenarioCreatedAt);

            INSERT INTO Runs (
                RunId,
                ScenarioId,
                ScenarioVersionId,
                Status,
                WaitReason,
                CursorJson,
                VariablesJson,
                CreatedAt,
                UpdatedAt)
            VALUES (
                $runId,
                $scenarioId,
                $versionId,
                'Waiting',
                'Retry',
                $cursorJson,
                '{}',
                $createdAt,
                $updatedAt);

            PRAGMA user_version = 2;
            """;

        command.Parameters.AddWithValue("$scenarioId", version.ScenarioId.ToString("D"));
        command.Parameters.AddWithValue("$versionId", version.VersionId.ToString("D"));
        command.Parameters.AddWithValue("$versionNumber", version.VersionNumber);
        command.Parameters.AddWithValue("$schemaVersion", version.SchemaVersion);
        command.Parameters.AddWithValue("$definitionHash", version.DefinitionHash);
        command.Parameters.AddWithValue("$definitionJson", version.DefinitionJson);
        command.Parameters.AddWithValue("$scenarioCreatedAt", version.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$runId", runId.ToString("D"));
        command.Parameters.AddWithValue("$cursorJson", ExecutionCursorJson.Serialize(cursor));
        command.Parameters.AddWithValue("$createdAt", createdAt.ToString("O"));
        command.Parameters.AddWithValue("$updatedAt", updatedAt.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }

    private static async Task CreateVersion1DatabaseAsync(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        await using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
            }.ToString());
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE ScenarioVersions (
                ScenarioId TEXT NOT NULL,
                VersionId TEXT NOT NULL PRIMARY KEY,
                VersionNumber INTEGER NOT NULL,
                SchemaVersion INTEGER NOT NULL,
                DefinitionHash TEXT NOT NULL,
                DefinitionJson TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UNIQUE (ScenarioId, VersionNumber)
            );

            CREATE TABLE Runs (
                RunId TEXT NOT NULL PRIMARY KEY,
                ScenarioId TEXT NOT NULL,
                ScenarioVersionId TEXT NOT NULL,
                Status TEXT NOT NULL,
                WaitReason TEXT NULL,
                CursorJson TEXT NOT NULL,
                VariablesJson TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                FOREIGN KEY (ScenarioVersionId)
                    REFERENCES ScenarioVersions(VersionId)
                    ON DELETE RESTRICT
            );

            CREATE INDEX IX_Runs_ScenarioVersionId
                ON Runs(ScenarioVersionId);

            PRAGMA user_version = 1;
            """;

        await command.ExecuteNonQueryAsync();
    }
}
