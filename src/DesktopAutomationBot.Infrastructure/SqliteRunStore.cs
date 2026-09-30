using System.Globalization;
using System.Text.Json;
using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using Microsoft.Data.Sqlite;

namespace DesktopAutomationBot.Infrastructure;

public sealed class SqliteRunStore : IRunStore, IStepAttemptStore, IRetryRunStore, IEventInboxStore, IPageObserverStore
{
    private const int StoreSchemaVersion = 6;

    private readonly string _connectionString;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private volatile bool _initialized;

    public SqliteRunStore(BotOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var databasePath = options.Storage.DatabasePath;
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var fullPath = Path.GetFullPath(databasePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
    }

    public async Task SaveAsync(
        AutomationRun run,
        ScenarioVersion scenarioVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(scenarioVersion);

        if (run.ScenarioId != scenarioVersion.ScenarioId ||
            run.ScenarioVersionId != scenarioVersion.VersionId)
        {
            throw new ArgumentException(
                "Run scenario identity must match the supplied immutable scenario version.",
                nameof(scenarioVersion));
        }

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await SaveScenarioVersionAsync(
            connection,
            transaction,
            scenarioVersion,
            cancellationToken);

        await SaveRunAsync(
            connection,
            transaction,
            run,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<StoredAutomationRun?> LoadAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("Run ID must not be empty.", nameof(runId));
        }

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                r.RunId,
                r.Status,
                r.WaitReason,
                r.CursorJson,
                r.VariablesJson,
                r.CreatedAt,
                r.UpdatedAt,
                r.RetryNotBefore,
                sv.ScenarioId,
                sv.VersionId,
                sv.VersionNumber,
                sv.SchemaVersion,
                sv.DefinitionHash,
                sv.DefinitionJson,
                sv.CreatedAt
            FROM Runs r
            INNER JOIN ScenarioVersions sv
                ON sv.VersionId = r.ScenarioVersionId
            WHERE r.RunId = $runId;
            """;
        command.Parameters.AddWithValue("$runId", runId.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var scenarioVersion = ScenarioVersion.Restore(
            ParseGuid(reader.GetString(8), "ScenarioId"),
            ParseGuid(reader.GetString(9), "VersionId"),
            reader.GetInt32(10),
            reader.GetInt32(11),
            reader.GetString(12),
            reader.GetString(13),
            ParseTimestamp(reader.GetString(14), "ScenarioVersion.CreatedAt"));

        var status = ParseEnum<RunStatus>(reader.GetString(1), "RunStatus");
        RunWaitReason? waitReason = reader.IsDBNull(2)
            ? null
            : ParseEnum<RunWaitReason>(reader.GetString(2), "RunWaitReason");

        var state = RunState.Restore(status, waitReason);
        var cursor = ExecutionCursorJson.Deserialize(reader.GetString(3));
        var variables = DeserializeVariables(reader.GetString(4));

        DateTimeOffset? retryNotBefore = reader.IsDBNull(7)
            ? null
            : ParseTimestamp(
                reader.GetString(7),
                "AutomationRun.RetryNotBefore");

        var run = AutomationRun.RestoreStructured(
            ParseGuid(reader.GetString(0), "RunId"),
            scenarioVersion,
            state,
            cursor,
            variables,
            ParseTimestamp(reader.GetString(5), "AutomationRun.CreatedAt"),
            ParseTimestamp(reader.GetString(6), "AutomationRun.UpdatedAt"),
            retryNotBefore);

        return new StoredAutomationRun(run, scenarioVersion);
    }

    public async Task<IReadOnlyList<Guid>> LoadDueRetryRunIdsAsync(
        DateTimeOffset dueAt,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                limit,
                "Limit must be greater than zero.");
        }

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT RunId
            FROM Runs
            WHERE Status = $waitingStatus
              AND WaitReason = $retryReason
              AND (
                    RetryNotBefore IS NULL
                    OR julianday(RetryNotBefore) <= julianday($dueAt)
                  )
            ORDER BY
                julianday(COALESCE(RetryNotBefore, UpdatedAt)),
                julianday(UpdatedAt),
                RunId
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue(
            "$waitingStatus",
            RunStatus.Waiting.ToString());
        command.Parameters.AddWithValue(
            "$retryReason",
            RunWaitReason.Retry.ToString());
        command.Parameters.AddWithValue(
            "$dueAt",
            FormatTimestamp(dueAt));
        command.Parameters.AddWithValue("$limit", limit);

        var runIds = new List<Guid>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            runIds.Add(ParseGuid(reader.GetString(0), "RunId"));
        }

        return runIds;
    }

    public async Task SaveStepAttemptAsync(
        StepAttempt attempt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        var validatedAttempt = RestoreAttempt(attempt);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        if (!await RunExistsAsync(
                connection,
                transaction,
                validatedAttempt.RunId,
                cancellationToken))
        {
            throw new InvalidOperationException(
                $"Run '{validatedAttempt.RunId}' must be persisted before its step attempts.");
        }

        var existing = await LoadStepAttemptByIdAsync(
            connection,
            transaction,
            validatedAttempt.AttemptId,
            cancellationToken);

        if (existing is null)
        {
            if (validatedAttempt.Status != StepAttemptStatus.Started)
            {
                throw new InvalidOperationException(
                    $"Step attempt '{validatedAttempt.AttemptId}' must be persisted as Started before it can be finalized.");
            }

            try
            {
                await InsertStepAttemptAsync(
                    connection,
                    transaction,
                    validatedAttempt,
                    cancellationToken);
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
            {
                throw new InvalidOperationException(
                    $"Step attempt number '{validatedAttempt.AttemptNumber}' already exists for run '{validatedAttempt.RunId}' and step '{validatedAttempt.StepId}', or its persisted identity conflicts.",
                    exception);
            }

            await transaction.CommitAsync(cancellationToken);
            return;
        }

        EnsureAttemptIdentityMatches(existing, validatedAttempt);

        if (existing == validatedAttempt)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        if (existing.Status != StepAttemptStatus.Started)
        {
            throw new InvalidOperationException(
                $"Step attempt '{validatedAttempt.AttemptId}' is already finalized as '{existing.Status}' and cannot be changed.");
        }

        if (validatedAttempt.Status == StepAttemptStatus.Started)
        {
            throw new InvalidOperationException(
                $"Persisted Started attempt '{validatedAttempt.AttemptId}' cannot be mutated without finalizing it.");
        }

        await FinalizeStepAttemptAsync(
            connection,
            transaction,
            validatedAttempt,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StepAttempt>> LoadStepAttemptsAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("Run ID must not be empty.", nameof(runId));
        }

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                AttemptId,
                RunId,
                StepId,
                StepType,
                AttemptNumber,
                RetrySafety,
                Status,
                StartedAt,
                UpdatedAt,
                FinishedAt,
                ErrorMessage
            FROM StepAttempts
            WHERE RunId = $runId
            ORDER BY StartedAt, AttemptNumber, AttemptId;
            """;
        command.Parameters.AddWithValue("$runId", runId.ToString("D"));

        var attempts = new List<StepAttempt>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            attempts.Add(ReadStepAttempt(reader));
        }

        return attempts;
    }

    public async Task<IReadOnlyList<StepAttempt>> MarkStartedAttemptsUnknownAsync(
        Guid runId,
        DateTimeOffset detectedAt,
        CancellationToken cancellationToken = default)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("Run ID must not be empty.", nameof(runId));
        }

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var startedAttempts = new List<StepAttempt>();

        await using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText =
                """
                SELECT
                    AttemptId,
                    RunId,
                    StepId,
                    StepType,
                    AttemptNumber,
                    RetrySafety,
                    Status,
                    StartedAt,
                    UpdatedAt,
                    FinishedAt,
                    ErrorMessage
                FROM StepAttempts
                WHERE RunId = $runId
                  AND Status = $startedStatus
                ORDER BY StartedAt, AttemptNumber, AttemptId;
                """;
            select.Parameters.AddWithValue("$runId", runId.ToString("D"));
            select.Parameters.AddWithValue("$startedStatus", StepAttemptStatus.Started.ToString());

            await using var reader = await select.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                startedAttempts.Add(ReadStepAttempt(reader));
            }
        }

        var interrupted = new List<StepAttempt>();

        foreach (var attempt in startedAttempts)
        {
            if (attempt.StartedAt > detectedAt)
            {
                continue;
            }

            var unknown = attempt.MarkUnknown(detectedAt);

            await using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText =
                """
                UPDATE StepAttempts
                SET
                    Status = $status,
                    UpdatedAt = $updatedAt,
                    FinishedAt = NULL,
                    ErrorMessage = NULL
                WHERE AttemptId = $attemptId
                  AND Status = $startedStatus;
                """;
            update.Parameters.AddWithValue("$status", StepAttemptStatus.Unknown.ToString());
            update.Parameters.AddWithValue("$updatedAt", FormatTimestamp(unknown.UpdatedAt));
            update.Parameters.AddWithValue("$attemptId", unknown.AttemptId.ToString("D"));
            update.Parameters.AddWithValue("$startedStatus", StepAttemptStatus.Started.ToString());

            var changed = await update.ExecuteNonQueryAsync(cancellationToken);
            if (changed == 1)
            {
                interrupted.Add(unknown);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return interrupted;
    }


    public async Task ArmEventWaitAsync(
        AutomationRun run,
        ScenarioVersion scenarioVersion,
        EventWaitRegistration wait,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(scenarioVersion);
        ArgumentNullException.ThrowIfNull(wait);

        if (run.RunId != wait.RunId)
        {
            throw new ArgumentException(
                "Event wait run identity must match the supplied run.",
                nameof(wait));
        }

        if (run.State.Status != RunStatus.Waiting ||
            run.State.WaitReason != RunWaitReason.Event)
        {
            throw new ArgumentException(
                "Event waits can only be armed for runs in Waiting / Event.",
                nameof(run));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(wait.CorrelationId);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await SaveScenarioVersionAsync(
            connection,
            transaction,
            scenarioVersion,
            cancellationToken);

        await SaveRunAsync(
            connection,
            transaction,
            run,
            cancellationToken);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO EventWaits (
                RunId,
                CorrelationId,
                EventType,
                CreatedAt)
            VALUES (
                $runId,
                $correlationId,
                $eventType,
                $createdAt)
            ON CONFLICT(RunId) DO UPDATE SET
                CorrelationId = excluded.CorrelationId,
                EventType = excluded.EventType,
                CreatedAt = excluded.CreatedAt;
            """;
        command.Parameters.AddWithValue(
            "$runId",
            wait.RunId.ToString("D"));
        command.Parameters.AddWithValue(
            "$correlationId",
            wait.CorrelationId.Trim());
        command.Parameters.AddWithValue(
            "$eventType",
            string.IsNullOrWhiteSpace(wait.EventType)
                ? DBNull.Value
                : wait.EventType.Trim());
        command.Parameters.AddWithValue(
            "$createdAt",
            FormatTimestamp(wait.CreatedAt));

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            throw new InvalidOperationException(
                $"Correlation ID '{wait.CorrelationId}' is already used by another active event wait.",
                exception);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<EventAcceptanceResult> AcceptAsync(
        AutomationEvent automationEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(automationEvent);

        var validated = AutomationEvent.Create(
            automationEvent.EventId,
            automationEvent.Type,
            automationEvent.CorrelationId,
            automationEvent.Payload,
            automationEvent.OccurredAt);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var duplicateCheck = connection.CreateCommand())
        {
            duplicateCheck.Transaction = transaction;
            duplicateCheck.CommandText =
                """
                SELECT
                    rw.RunId,
                    rw.WorkItemId
                FROM AutomationEvents e
                LEFT JOIN ResumeWorkItems rw
                    ON rw.EventId = e.EventId
                WHERE e.EventId = $eventId;
                """;
            duplicateCheck.Parameters.AddWithValue(
                "$eventId",
                validated.EventId.ToString("D"));

            await using var reader =
                await duplicateCheck.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync(cancellationToken))
            {
                var duplicateRunId = reader.IsDBNull(0)
                    ? (Guid?)null
                    : ParseGuid(reader.GetString(0), "ResumeWorkItem.RunId");
                var duplicateWorkItemId = reader.IsDBNull(1)
                    ? (Guid?)null
                    : ParseGuid(reader.GetString(1), "ResumeWorkItem.WorkItemId");

                await transaction.CommitAsync(cancellationToken);

                return new EventAcceptanceResult
                {
                    IsDuplicate = true,
                    MatchedWaitingRun = duplicateRunId is not null,
                    RunId = duplicateRunId,
                    ResumeWorkItemId = duplicateWorkItemId,
                };
            }
        }

        await using (var insertEvent = connection.CreateCommand())
        {
            insertEvent.Transaction = transaction;
            insertEvent.CommandText =
                """
                INSERT INTO AutomationEvents (
                    EventId,
                    Type,
                    CorrelationId,
                    PayloadJson,
                    OccurredAt,
                    ReceivedAt)
                VALUES (
                    $eventId,
                    $type,
                    $correlationId,
                    $payloadJson,
                    $occurredAt,
                    $receivedAt);
                """;
            insertEvent.Parameters.AddWithValue(
                "$eventId",
                validated.EventId.ToString("D"));
            insertEvent.Parameters.AddWithValue("$type", validated.Type);
            insertEvent.Parameters.AddWithValue(
                "$correlationId",
                validated.CorrelationId);
            insertEvent.Parameters.AddWithValue(
                "$payloadJson",
                validated.Payload.ToJsonElement().GetRawText());
            insertEvent.Parameters.AddWithValue(
                "$occurredAt",
                FormatTimestamp(validated.OccurredAt));
            insertEvent.Parameters.AddWithValue(
                "$receivedAt",
                FormatTimestamp(DateTimeOffset.UtcNow));

            await insertEvent.ExecuteNonQueryAsync(cancellationToken);
        }

        Guid? runId = null;

        await using (var findWait = connection.CreateCommand())
        {
            findWait.Transaction = transaction;
            findWait.CommandText =
                """
                SELECT ew.RunId
                FROM EventWaits ew
                INNER JOIN Runs r
                    ON r.RunId = ew.RunId
                WHERE ew.CorrelationId = $correlationId
                  AND (ew.EventType IS NULL OR ew.EventType = $eventType)
                  AND r.Status = $waitingStatus
                  AND r.WaitReason = $eventReason
                LIMIT 1;
                """;
            findWait.Parameters.AddWithValue(
                "$correlationId",
                validated.CorrelationId);
            findWait.Parameters.AddWithValue(
                "$eventType",
                validated.Type);
            findWait.Parameters.AddWithValue(
                "$waitingStatus",
                RunStatus.Waiting.ToString());
            findWait.Parameters.AddWithValue(
                "$eventReason",
                RunWaitReason.Event.ToString());

            var rawRunId = await findWait.ExecuteScalarAsync(cancellationToken);
            if (rawRunId is string runIdText)
            {
                runId = ParseGuid(runIdText, "EventWait.RunId");
            }
        }

        Guid? workItemId = null;
        if (runId is { } matchedRunId)
        {
            await using (var loadVariables = connection.CreateCommand())
            {
                loadVariables.Transaction = transaction;
                loadVariables.CommandText =
                    """
                    SELECT VariablesJson
                    FROM Runs
                    WHERE RunId = $runId;
                    """;
                loadVariables.Parameters.AddWithValue(
                    "$runId",
                    matchedRunId.ToString("D"));

                var rawVariables =
                    await loadVariables.ExecuteScalarAsync(cancellationToken)
                    as string
                    ?? throw new InvalidOperationException(
                        $"Matched run '{matchedRunId}' no longer exists.");

                var eventVariables =
                    DeserializeVariables(rawVariables);
                eventVariables["event.id"] =
                    ScenarioVariableValue.FromString(
                        validated.EventId.ToString("D"));
                eventVariables["event.type"] =
                    ScenarioVariableValue.FromString(validated.Type);
                eventVariables["event.correlationId"] =
                    ScenarioVariableValue.FromString(
                        validated.CorrelationId);
                eventVariables["event.payload"] =
                    ScenarioVariableValue.FromJsonElement(
                        validated.Payload.ToJsonElement());
                eventVariables["event.occurredAt"] =
                    ScenarioVariableValue.FromString(
                        FormatTimestamp(validated.OccurredAt));

                await using var updateVariables =
                    connection.CreateCommand();
                updateVariables.Transaction = transaction;
                updateVariables.CommandText =
                    """
                    UPDATE Runs
                    SET
                        VariablesJson = $variablesJson,
                        UpdatedAt = $updatedAt
                    WHERE RunId = $runId;
                    """;
                updateVariables.Parameters.AddWithValue(
                    "$variablesJson",
                    SerializeVariables(eventVariables));
                updateVariables.Parameters.AddWithValue(
                    "$updatedAt",
                    FormatTimestamp(DateTimeOffset.UtcNow));
                updateVariables.Parameters.AddWithValue(
                    "$runId",
                    matchedRunId.ToString("D"));

                await updateVariables.ExecuteNonQueryAsync(
                    cancellationToken);
            }

            workItemId = Guid.NewGuid();

            await using (var insertWork = connection.CreateCommand())
            {
                insertWork.Transaction = transaction;
                insertWork.CommandText =
                    """
                    INSERT INTO ResumeWorkItems (
                        WorkItemId,
                        RunId,
                        EventId,
                        Status,
                        CreatedAt,
                        FinishedAt,
                        ErrorMessage)
                    VALUES (
                        $workItemId,
                        $runId,
                        $eventId,
                        $status,
                        $createdAt,
                        NULL,
                        NULL);
                    """;
                insertWork.Parameters.AddWithValue(
                    "$workItemId",
                    workItemId.Value.ToString("D"));
                insertWork.Parameters.AddWithValue(
                    "$runId",
                    matchedRunId.ToString("D"));
                insertWork.Parameters.AddWithValue(
                    "$eventId",
                    validated.EventId.ToString("D"));
                insertWork.Parameters.AddWithValue(
                    "$status",
                    ResumeWorkItemStatus.Pending.ToString());
                insertWork.Parameters.AddWithValue(
                    "$createdAt",
                    FormatTimestamp(DateTimeOffset.UtcNow));

                await insertWork.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var deleteWait = connection.CreateCommand();
            deleteWait.Transaction = transaction;
            deleteWait.CommandText =
                """
                DELETE FROM EventWaits
                WHERE RunId = $runId;
                """;
            deleteWait.Parameters.AddWithValue(
                "$runId",
                matchedRunId.ToString("D"));
            await deleteWait.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new EventAcceptanceResult
        {
            IsDuplicate = false,
            MatchedWaitingRun = runId is not null,
            RunId = runId,
            ResumeWorkItemId = workItemId,
        };
    }

    public async Task<IReadOnlyList<ResumeWorkItem>> LoadPendingResumeWorkItemsAsync(
        DateTimeOffset dueAt,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                limit,
                "Limit must be greater than zero.");
        }

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                WorkItemId,
                RunId,
                EventId,
                Status,
                CreatedAt,
                AttemptCount,
                NextAttemptAt,
                FinishedAt,
                ErrorMessage
            FROM ResumeWorkItems
            WHERE Status = $pendingStatus
              AND (NextAttemptAt IS NULL OR NextAttemptAt <= $dueAt)
            ORDER BY
                COALESCE(NextAttemptAt, CreatedAt),
                CreatedAt,
                WorkItemId
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue(
            "$pendingStatus",
            ResumeWorkItemStatus.Pending.ToString());
        command.Parameters.AddWithValue(
            "$dueAt",
            FormatTimestamp(dueAt));
        command.Parameters.AddWithValue("$limit", limit);

        var items = new List<ResumeWorkItem>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadResumeWorkItem(reader));
        }

        return items;
    }

    public async Task<IReadOnlyList<ResumeWorkItem>> LoadDeadLetterResumeWorkItemsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                limit,
                "Limit must be greater than zero.");
        }

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                WorkItemId,
                RunId,
                EventId,
                Status,
                CreatedAt,
                AttemptCount,
                NextAttemptAt,
                FinishedAt,
                ErrorMessage
            FROM ResumeWorkItems
            WHERE Status = $deadLetterStatus
            ORDER BY FinishedAt DESC, CreatedAt, WorkItemId
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue(
            "$deadLetterStatus",
            ResumeWorkItemStatus.DeadLetter.ToString());
        command.Parameters.AddWithValue("$limit", limit);

        var items = new List<ResumeWorkItem>();

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadResumeWorkItem(reader));
        }

        return items;
    }

    public async Task MarkResumeWorkItemCompletedAsync(
        Guid workItemId,
        DateTimeOffset finishedAt,
        CancellationToken cancellationToken = default)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException(
                "Work item ID must not be empty.",
                nameof(workItemId));
        }

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE ResumeWorkItems
            SET
                Status = $status,
                AttemptCount = AttemptCount + 1,
                NextAttemptAt = NULL,
                FinishedAt = $finishedAt,
                ErrorMessage = NULL
            WHERE WorkItemId = $workItemId
              AND Status = $pendingStatus;
            """;
        command.Parameters.AddWithValue(
            "$status",
            ResumeWorkItemStatus.Completed.ToString());
        command.Parameters.AddWithValue(
            "$finishedAt",
            FormatTimestamp(finishedAt));
        command.Parameters.AddWithValue(
            "$workItemId",
            workItemId.ToString("D"));
        command.Parameters.AddWithValue(
            "$pendingStatus",
            ResumeWorkItemStatus.Pending.ToString());

        await EnsureSingleWorkItemUpdateAsync(
            command,
            workItemId,
            cancellationToken);
    }

    public async Task ScheduleResumeWorkItemRetryAsync(
        Guid workItemId,
        string errorMessage,
        DateTimeOffset nextAttemptAt,
        CancellationToken cancellationToken = default)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException(
                "Work item ID must not be empty.",
                nameof(workItemId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE ResumeWorkItems
            SET
                AttemptCount = AttemptCount + 1,
                NextAttemptAt = $nextAttemptAt,
                ErrorMessage = $errorMessage
            WHERE WorkItemId = $workItemId
              AND Status = $pendingStatus;
            """;
        command.Parameters.AddWithValue(
            "$nextAttemptAt",
            FormatTimestamp(nextAttemptAt));
        command.Parameters.AddWithValue(
            "$errorMessage",
            errorMessage);
        command.Parameters.AddWithValue(
            "$workItemId",
            workItemId.ToString("D"));
        command.Parameters.AddWithValue(
            "$pendingStatus",
            ResumeWorkItemStatus.Pending.ToString());

        await EnsureSingleWorkItemUpdateAsync(
            command,
            workItemId,
            cancellationToken);
    }

    public async Task DeadLetterResumeWorkItemAsync(
        Guid workItemId,
        string errorMessage,
        DateTimeOffset finishedAt,
        CancellationToken cancellationToken = default)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException(
                "Work item ID must not be empty.",
                nameof(workItemId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE ResumeWorkItems
            SET
                Status = $status,
                AttemptCount = AttemptCount + 1,
                NextAttemptAt = NULL,
                FinishedAt = $finishedAt,
                ErrorMessage = $errorMessage
            WHERE WorkItemId = $workItemId
              AND Status = $pendingStatus;
            """;
        command.Parameters.AddWithValue(
            "$status",
            ResumeWorkItemStatus.DeadLetter.ToString());
        command.Parameters.AddWithValue(
            "$finishedAt",
            FormatTimestamp(finishedAt));
        command.Parameters.AddWithValue(
            "$errorMessage",
            errorMessage);
        command.Parameters.AddWithValue(
            "$workItemId",
            workItemId.ToString("D"));
        command.Parameters.AddWithValue(
            "$pendingStatus",
            ResumeWorkItemStatus.Pending.ToString());

        await EnsureSingleWorkItemUpdateAsync(
            command,
            workItemId,
            cancellationToken);
    }

    private static async Task EnsureSingleWorkItemUpdateAsync(
        SqliteCommand command,
        Guid workItemId,
        CancellationToken cancellationToken)
    {
        var changed = await command.ExecuteNonQueryAsync(cancellationToken);
        if (changed != 1)
        {
            throw new InvalidOperationException(
                $"Pending resume work item '{workItemId}' does not exist or was already finalized.");
        }
    }

    private static ResumeWorkItem ReadResumeWorkItem(
        SqliteDataReader reader) =>
        new()
        {
            WorkItemId = ParseGuid(
                reader.GetString(0),
                "ResumeWorkItem.WorkItemId"),
            RunId = ParseGuid(
                reader.GetString(1),
                "ResumeWorkItem.RunId"),
            EventId = ParseGuid(
                reader.GetString(2),
                "ResumeWorkItem.EventId"),
            Status = ParseEnum<ResumeWorkItemStatus>(
                reader.GetString(3),
                "ResumeWorkItem.Status"),
            CreatedAt = ParseTimestamp(
                reader.GetString(4),
                "ResumeWorkItem.CreatedAt"),
            AttemptCount = reader.GetInt32(5),
            NextAttemptAt = reader.IsDBNull(6)
                ? null
                : ParseTimestamp(
                    reader.GetString(6),
                    "ResumeWorkItem.NextAttemptAt"),
            FinishedAt = reader.IsDBNull(7)
                ? null
                : ParseTimestamp(
                    reader.GetString(7),
                    "ResumeWorkItem.FinishedAt"),
            ErrorMessage = reader.IsDBNull(8)
                ? null
                : reader.GetString(8),
        };

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using var connection = await OpenConnectionAsync(cancellationToken);

            await using var versionCommand = connection.CreateCommand();
            versionCommand.CommandText = "PRAGMA user_version;";
            var rawVersion = await versionCommand.ExecuteScalarAsync(cancellationToken);
            var currentVersion = Convert.ToInt32(rawVersion, CultureInfo.InvariantCulture);

            if (currentVersion > StoreSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"SQLite run store schema version '{currentVersion}' is newer than supported version '{StoreSchemaVersion}'.");
            }

            if (currentVersion == 0)
            {
                await CreateSchemaAsync(connection, cancellationToken);
            }
            else if (currentVersion < StoreSchemaVersion)
            {
                await UpgradeSchemaAsync(
                    connection,
                    currentVersion,
                    cancellationToken);
            }

            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task<SqliteConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var pragma = connection.CreateCommand();
        pragma.CommandText =
            """
            PRAGMA foreign_keys = ON;
            PRAGMA busy_timeout = 5000;
            """;
        await pragma.ExecuteNonQueryAsync(cancellationToken);

        return connection;
    }

    private static async Task CreateSchemaAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
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
                RetryNotBefore TEXT NULL,
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

            CREATE TABLE AutomationEvents (
                EventId TEXT NOT NULL PRIMARY KEY,
                Type TEXT NOT NULL,
                CorrelationId TEXT NOT NULL,
                PayloadJson TEXT NOT NULL,
                OccurredAt TEXT NOT NULL,
                ReceivedAt TEXT NOT NULL
            );

            CREATE INDEX IX_AutomationEvents_CorrelationId
                ON AutomationEvents(CorrelationId);

            CREATE TABLE EventWaits (
                RunId TEXT NOT NULL PRIMARY KEY,
                CorrelationId TEXT NOT NULL,
                EventType TEXT NULL,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (RunId)
                    REFERENCES Runs(RunId)
                    ON DELETE CASCADE
            );

            CREATE UNIQUE INDEX UX_EventWaits_Correlation
                ON EventWaits(CorrelationId);

            CREATE TABLE ResumeWorkItems (
                WorkItemId TEXT NOT NULL PRIMARY KEY,
                RunId TEXT NOT NULL,
                EventId TEXT NOT NULL,
                Status TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                AttemptCount INTEGER NOT NULL DEFAULT 0,
                NextAttemptAt TEXT NULL,
                FinishedAt TEXT NULL,
                ErrorMessage TEXT NULL,
                UNIQUE (EventId),
                FOREIGN KEY (RunId)
                    REFERENCES Runs(RunId)
                    ON DELETE CASCADE,
                FOREIGN KEY (EventId)
                    REFERENCES AutomationEvents(EventId)
                    ON DELETE CASCADE
            );

            CREATE INDEX IX_ResumeWorkItems_Status_NextAttemptAt
                ON ResumeWorkItems(Status, NextAttemptAt, CreatedAt);

            CREATE TABLE PageObservers (
                ObserverId TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL,
                Url TEXT NOT NULL,
                BrowserProfile TEXT NULL,
                Condition TEXT NOT NULL,
                LocatorJson TEXT NULL,
                ExpectedValue TEXT NULL,
                EventType TEXT NOT NULL,
                CorrelationId TEXT NOT NULL,
                PollIntervalMs INTEGER NOT NULL CHECK (PollIntervalMs > 0),
                Enabled INTEGER NOT NULL CHECK (Enabled IN (0, 1))
            );

            CREATE TABLE PageObserverSnapshots (
                ObserverId TEXT NOT NULL PRIMARY KEY,
                LastObservation TEXT NULL,
                LastMatched INTEGER NULL CHECK (LastMatched IN (0, 1)),
                LastCheckedAt TEXT NULL,
                NextCheckAt TEXT NULL,
                LastEventAt TEXT NULL,
                FailureCount INTEGER NOT NULL DEFAULT 0 CHECK (FailureCount >= 0),
                LastError TEXT NULL,
                FOREIGN KEY (ObserverId)
                    REFERENCES PageObservers(ObserverId)
                    ON DELETE CASCADE
            );

            CREATE INDEX IX_PageObservers_Enabled
                ON PageObservers(Enabled);

            CREATE INDEX IX_PageObserverSnapshots_NextCheckAt
                ON PageObserverSnapshots(NextCheckAt);

            PRAGMA user_version = 6;
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task UpgradeSchemaAsync(
        SqliteConnection connection,
        int currentVersion,
        CancellationToken cancellationToken)
    {
        var version = currentVersion;

        while (version < StoreSchemaVersion)
        {
            switch (version)
            {
                case 1:
                    await MigrateV1ToV2Async(connection, cancellationToken);
                    version = 2;
                    break;

                case 2:
                    await MigrateV2ToV3Async(connection, cancellationToken);
                    version = 3;
                    break;

                case 3:
                    await MigrateV3ToV4Async(connection, cancellationToken);
                    version = 4;
                    break;

                case 4:
                    await MigrateV4ToV5Async(connection, cancellationToken);
                    version = 5;
                    break;

                case 5:
                    await MigrateV5ToV6Async(connection, cancellationToken);
                    version = 6;
                    break;

                default:
                    throw new InvalidOperationException(
                        $"SQLite run store schema version '{version}' cannot be upgraded to '{StoreSchemaVersion}'.");
            }
        }
    }

    private static async Task MigrateV1ToV2Async(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
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

            PRAGMA user_version = 2;
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task MigrateV2ToV3Async(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(
                cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            ALTER TABLE Runs
                ADD COLUMN RetryNotBefore TEXT NULL;

            UPDATE Runs
            SET RetryNotBefore = UpdatedAt
            WHERE Status = 'Waiting'
              AND WaitReason = 'Retry'
              AND RetryNotBefore IS NULL;

            PRAGMA user_version = 3;
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task MigrateV3ToV4Async(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(
                cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            CREATE TABLE AutomationEvents (
                EventId TEXT NOT NULL PRIMARY KEY,
                Type TEXT NOT NULL,
                CorrelationId TEXT NOT NULL,
                PayloadJson TEXT NOT NULL,
                OccurredAt TEXT NOT NULL,
                ReceivedAt TEXT NOT NULL
            );

            CREATE INDEX IX_AutomationEvents_CorrelationId
                ON AutomationEvents(CorrelationId);

            CREATE TABLE EventWaits (
                RunId TEXT NOT NULL PRIMARY KEY,
                CorrelationId TEXT NOT NULL,
                EventType TEXT NULL,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (RunId)
                    REFERENCES Runs(RunId)
                    ON DELETE CASCADE
            );

            CREATE UNIQUE INDEX UX_EventWaits_Correlation
                ON EventWaits(CorrelationId);

            CREATE TABLE ResumeWorkItems (
                WorkItemId TEXT NOT NULL PRIMARY KEY,
                RunId TEXT NOT NULL,
                EventId TEXT NOT NULL,
                Status TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                FinishedAt TEXT NULL,
                ErrorMessage TEXT NULL,
                UNIQUE (EventId),
                FOREIGN KEY (RunId)
                    REFERENCES Runs(RunId)
                    ON DELETE CASCADE,
                FOREIGN KEY (EventId)
                    REFERENCES AutomationEvents(EventId)
                    ON DELETE CASCADE
            );

            CREATE INDEX IX_ResumeWorkItems_Status_CreatedAt
                ON ResumeWorkItems(Status, CreatedAt);

            PRAGMA user_version = 4;
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task MigrateV4ToV5Async(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(
                cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            ALTER TABLE ResumeWorkItems
                ADD COLUMN AttemptCount INTEGER NOT NULL DEFAULT 0;

            ALTER TABLE ResumeWorkItems
                ADD COLUMN NextAttemptAt TEXT NULL;

            DROP INDEX IX_ResumeWorkItems_Status_CreatedAt;

            CREATE INDEX IX_ResumeWorkItems_Status_NextAttemptAt
                ON ResumeWorkItems(Status, NextAttemptAt, CreatedAt);

            PRAGMA user_version = 5;
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task MigrateV5ToV6Async(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(
                cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            CREATE TABLE PageObservers (
                ObserverId TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL,
                Url TEXT NOT NULL,
                BrowserProfile TEXT NULL,
                Condition TEXT NOT NULL,
                LocatorJson TEXT NULL,
                ExpectedValue TEXT NULL,
                EventType TEXT NOT NULL,
                CorrelationId TEXT NOT NULL,
                PollIntervalMs INTEGER NOT NULL CHECK (PollIntervalMs > 0),
                Enabled INTEGER NOT NULL CHECK (Enabled IN (0, 1))
            );

            CREATE TABLE PageObserverSnapshots (
                ObserverId TEXT NOT NULL PRIMARY KEY,
                LastObservation TEXT NULL,
                LastMatched INTEGER NULL CHECK (LastMatched IN (0, 1)),
                LastCheckedAt TEXT NULL,
                NextCheckAt TEXT NULL,
                LastEventAt TEXT NULL,
                FailureCount INTEGER NOT NULL DEFAULT 0 CHECK (FailureCount >= 0),
                LastError TEXT NULL,
                FOREIGN KEY (ObserverId)
                    REFERENCES PageObservers(ObserverId)
                    ON DELETE CASCADE
            );

            CREATE INDEX IX_PageObservers_Enabled
                ON PageObservers(Enabled);

            CREATE INDEX IX_PageObserverSnapshots_NextCheckAt
                ON PageObserverSnapshots(NextCheckAt);

            PRAGMA user_version = 6;
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task SaveScenarioVersionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ScenarioVersion scenarioVersion,
        CancellationToken cancellationToken)
    {
        await using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText =
            """
            INSERT OR IGNORE INTO ScenarioVersions (
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
                $createdAt);
            """;
        insert.Parameters.AddWithValue("$scenarioId", scenarioVersion.ScenarioId.ToString("D"));
        insert.Parameters.AddWithValue("$versionId", scenarioVersion.VersionId.ToString("D"));
        insert.Parameters.AddWithValue("$versionNumber", scenarioVersion.VersionNumber);
        insert.Parameters.AddWithValue("$schemaVersion", scenarioVersion.SchemaVersion);
        insert.Parameters.AddWithValue("$definitionHash", scenarioVersion.DefinitionHash);
        insert.Parameters.AddWithValue("$definitionJson", scenarioVersion.DefinitionJson);
        insert.Parameters.AddWithValue("$createdAt", FormatTimestamp(scenarioVersion.CreatedAt));

        await insert.ExecuteNonQueryAsync(cancellationToken);

        await using var verify = connection.CreateCommand();
        verify.Transaction = transaction;
        verify.CommandText =
            """
            SELECT
                ScenarioId,
                VersionNumber,
                SchemaVersion,
                DefinitionHash,
                DefinitionJson,
                CreatedAt
            FROM ScenarioVersions
            WHERE VersionId = $versionId;
            """;
        verify.Parameters.AddWithValue("$versionId", scenarioVersion.VersionId.ToString("D"));

        await using var reader = await verify.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "Scenario version could not be stored. The scenario/version number may already identify a different immutable version.");
        }

        var matches =
            ParseGuid(reader.GetString(0), "ScenarioId") == scenarioVersion.ScenarioId &&
            reader.GetInt32(1) == scenarioVersion.VersionNumber &&
            reader.GetInt32(2) == scenarioVersion.SchemaVersion &&
            string.Equals(reader.GetString(3), scenarioVersion.DefinitionHash, StringComparison.Ordinal) &&
            string.Equals(reader.GetString(4), scenarioVersion.DefinitionJson, StringComparison.Ordinal) &&
            ParseTimestamp(reader.GetString(5), "ScenarioVersion.CreatedAt") == scenarioVersion.CreatedAt;

        if (!matches)
        {
            throw new InvalidOperationException(
                $"Scenario version '{scenarioVersion.VersionId}' already exists with different immutable data.");
        }
    }

    private static async Task SaveRunAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        AutomationRun run,
        CancellationToken cancellationToken)
    {
        await using var identityCheck = connection.CreateCommand();
        identityCheck.Transaction = transaction;
        identityCheck.CommandText =
            """
            SELECT ScenarioId, ScenarioVersionId, CreatedAt
            FROM Runs
            WHERE RunId = $runId;
            """;
        identityCheck.Parameters.AddWithValue("$runId", run.RunId.ToString("D"));

        await using (var reader = await identityCheck.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                var identityMatches =
                    ParseGuid(reader.GetString(0), "ScenarioId") == run.ScenarioId &&
                    ParseGuid(reader.GetString(1), "ScenarioVersionId") == run.ScenarioVersionId &&
                    ParseTimestamp(reader.GetString(2), "AutomationRun.CreatedAt") == run.CreatedAt;

                if (!identityMatches)
                {
                    throw new InvalidOperationException(
                        $"Run '{run.RunId}' already exists with different immutable identity data.");
                }
            }
        }

        await using var upsert = connection.CreateCommand();
        upsert.Transaction = transaction;
        upsert.CommandText =
            """
            INSERT INTO Runs (
                RunId,
                ScenarioId,
                ScenarioVersionId,
                Status,
                WaitReason,
                RetryNotBefore,
                CursorJson,
                VariablesJson,
                CreatedAt,
                UpdatedAt)
            VALUES (
                $runId,
                $scenarioId,
                $scenarioVersionId,
                $status,
                $waitReason,
                $retryNotBefore,
                $cursorJson,
                $variablesJson,
                $createdAt,
                $updatedAt)
            ON CONFLICT(RunId) DO UPDATE SET
                Status = excluded.Status,
                WaitReason = excluded.WaitReason,
                RetryNotBefore = excluded.RetryNotBefore,
                CursorJson = excluded.CursorJson,
                VariablesJson = excluded.VariablesJson,
                UpdatedAt = excluded.UpdatedAt;
            """;

        upsert.Parameters.AddWithValue("$runId", run.RunId.ToString("D"));
        upsert.Parameters.AddWithValue("$scenarioId", run.ScenarioId.ToString("D"));
        upsert.Parameters.AddWithValue("$scenarioVersionId", run.ScenarioVersionId.ToString("D"));
        upsert.Parameters.AddWithValue("$status", run.State.Status.ToString());
        upsert.Parameters.AddWithValue(
            "$waitReason",
            run.State.WaitReason?.ToString() is { } reason
                ? reason
                : DBNull.Value);
        upsert.Parameters.AddWithValue(
            "$retryNotBefore",
            run.RetryNotBefore is { } retryNotBefore
                ? FormatTimestamp(retryNotBefore)
                : DBNull.Value);
        upsert.Parameters.AddWithValue("$cursorJson", ExecutionCursorJson.Serialize(run.Cursor));
        upsert.Parameters.AddWithValue("$variablesJson", SerializeVariables(run.Variables));
        upsert.Parameters.AddWithValue("$createdAt", FormatTimestamp(run.CreatedAt));
        upsert.Parameters.AddWithValue("$updatedAt", FormatTimestamp(run.UpdatedAt));

        await upsert.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> RunExistsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid runId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT 1
            FROM Runs
            WHERE RunId = $runId;
            """;
        command.Parameters.AddWithValue("$runId", runId.ToString("D"));

        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<StepAttempt?> LoadStepAttemptByIdAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT
                AttemptId,
                RunId,
                StepId,
                StepType,
                AttemptNumber,
                RetrySafety,
                Status,
                StartedAt,
                UpdatedAt,
                FinishedAt,
                ErrorMessage
            FROM StepAttempts
            WHERE AttemptId = $attemptId;
            """;
        command.Parameters.AddWithValue("$attemptId", attemptId.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadStepAttempt(reader)
            : null;
    }

    private static async Task InsertStepAttemptAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        StepAttempt attempt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO StepAttempts (
                AttemptId,
                RunId,
                StepId,
                StepType,
                AttemptNumber,
                RetrySafety,
                Status,
                StartedAt,
                UpdatedAt,
                FinishedAt,
                ErrorMessage)
            VALUES (
                $attemptId,
                $runId,
                $stepId,
                $stepType,
                $attemptNumber,
                $retrySafety,
                $status,
                $startedAt,
                $updatedAt,
                $finishedAt,
                $errorMessage);
            """;
        AddStepAttemptParameters(command, attempt);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task FinalizeStepAttemptAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        StepAttempt attempt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE StepAttempts
            SET
                Status = $status,
                UpdatedAt = $updatedAt,
                FinishedAt = $finishedAt,
                ErrorMessage = $errorMessage
            WHERE AttemptId = $attemptId
              AND Status = $startedStatus;
            """;
        command.Parameters.AddWithValue("$status", attempt.Status.ToString());
        command.Parameters.AddWithValue("$updatedAt", FormatTimestamp(attempt.UpdatedAt));
        command.Parameters.AddWithValue(
            "$finishedAt",
            attempt.FinishedAt is { } finishedAt
                ? FormatTimestamp(finishedAt)
                : DBNull.Value);
        command.Parameters.AddWithValue(
            "$errorMessage",
            attempt.ErrorMessage is { } errorMessage
                ? errorMessage
                : DBNull.Value);
        command.Parameters.AddWithValue("$attemptId", attempt.AttemptId.ToString("D"));
        command.Parameters.AddWithValue("$startedStatus", StepAttemptStatus.Started.ToString());

        var changed = await command.ExecuteNonQueryAsync(cancellationToken);
        if (changed != 1)
        {
            throw new InvalidOperationException(
                $"Step attempt '{attempt.AttemptId}' was concurrently finalized and could not be updated.");
        }
    }

    private static void AddStepAttemptParameters(
        SqliteCommand command,
        StepAttempt attempt)
    {
        command.Parameters.AddWithValue("$attemptId", attempt.AttemptId.ToString("D"));
        command.Parameters.AddWithValue("$runId", attempt.RunId.ToString("D"));
        command.Parameters.AddWithValue("$stepId", attempt.StepId);
        command.Parameters.AddWithValue("$stepType", attempt.StepType.ToString());
        command.Parameters.AddWithValue("$attemptNumber", attempt.AttemptNumber);
        command.Parameters.AddWithValue("$retrySafety", attempt.RetrySafety.ToString());
        command.Parameters.AddWithValue("$status", attempt.Status.ToString());
        command.Parameters.AddWithValue("$startedAt", FormatTimestamp(attempt.StartedAt));
        command.Parameters.AddWithValue("$updatedAt", FormatTimestamp(attempt.UpdatedAt));
        command.Parameters.AddWithValue(
            "$finishedAt",
            attempt.FinishedAt is { } finishedAt
                ? FormatTimestamp(finishedAt)
                : DBNull.Value);
        command.Parameters.AddWithValue(
            "$errorMessage",
            attempt.ErrorMessage is { } errorMessage
                ? errorMessage
                : DBNull.Value);
    }

    private static StepAttempt ReadStepAttempt(SqliteDataReader reader) =>
        StepAttempt.Restore(
            ParseGuid(reader.GetString(0), "StepAttempt.AttemptId"),
            ParseGuid(reader.GetString(1), "StepAttempt.RunId"),
            reader.GetString(2),
            ParseEnum<StepType>(reader.GetString(3), "StepAttempt.StepType"),
            reader.GetInt32(4),
            ParseEnum<StepRetrySafety>(reader.GetString(5), "StepAttempt.RetrySafety"),
            ParseEnum<StepAttemptStatus>(reader.GetString(6), "StepAttempt.Status"),
            ParseTimestamp(reader.GetString(7), "StepAttempt.StartedAt"),
            ParseTimestamp(reader.GetString(8), "StepAttempt.UpdatedAt"),
            reader.IsDBNull(9)
                ? null
                : ParseTimestamp(reader.GetString(9), "StepAttempt.FinishedAt"),
            reader.IsDBNull(10)
                ? null
                : reader.GetString(10));

    private static StepAttempt RestoreAttempt(StepAttempt attempt) =>
        StepAttempt.Restore(
            attempt.AttemptId,
            attempt.RunId,
            attempt.StepId,
            attempt.StepType,
            attempt.AttemptNumber,
            attempt.RetrySafety,
            attempt.Status,
            attempt.StartedAt,
            attempt.UpdatedAt,
            attempt.FinishedAt,
            attempt.ErrorMessage);

    private static void EnsureAttemptIdentityMatches(
        StepAttempt persisted,
        StepAttempt candidate)
    {
        var matches =
            persisted.AttemptId == candidate.AttemptId &&
            persisted.RunId == candidate.RunId &&
            string.Equals(persisted.StepId, candidate.StepId, StringComparison.Ordinal) &&
            persisted.StepType == candidate.StepType &&
            persisted.AttemptNumber == candidate.AttemptNumber &&
            persisted.RetrySafety == candidate.RetrySafety &&
            persisted.StartedAt == candidate.StartedAt;

        if (!matches)
        {
            throw new InvalidOperationException(
                $"Step attempt '{candidate.AttemptId}' already exists with different immutable identity data.");
        }
    }

    private static string SerializeVariables(
        IReadOnlyDictionary<string, ScenarioVariableValue> variables)
    {
        var serializable = variables.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToJsonElement(),
            StringComparer.OrdinalIgnoreCase);

        return JsonSerializer.Serialize(serializable);
    }

    private static Dictionary<string, ScenarioVariableValue> DeserializeVariables(
        string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                "Stored run variables must be a JSON object.");
        }

        var values = new Dictionary<string, ScenarioVariableValue>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var property in document.RootElement.EnumerateObject())
        {
            values.Add(
                property.Name,
                ScenarioVariableValue.FromJsonElement(property.Value));
        }

        return values;
    }

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTimestamp(
        string value,
        string fieldName)
    {
        if (DateTimeOffset.TryParseExact(
            value,
            "O",
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var timestamp))
        {
            return timestamp;
        }

        throw new InvalidOperationException(
            $"Stored field '{fieldName}' does not contain a valid round-trip timestamp.");
    }

    private static Guid ParseGuid(string value, string fieldName)
    {
        if (Guid.TryParse(value, out var guid) && guid != Guid.Empty)
        {
            return guid;
        }

        throw new InvalidOperationException(
            $"Stored field '{fieldName}' does not contain a valid non-empty GUID.");
    }

    private static TEnum ParseEnum<TEnum>(
        string value,
        string fieldName)
        where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(value, ignoreCase: false, out var parsed) &&
            Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException(
            $"Stored field '{fieldName}' contains unsupported value '{value}'.");
    }
}
