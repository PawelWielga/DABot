using System.Globalization;
using System.Text.Json;
using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using Microsoft.Data.Sqlite;

namespace DesktopAutomationBot.Infrastructure;

public sealed class SqliteRunStore : IRunStore
{
    private const int StoreSchemaVersion = 1;

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
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

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
            ParseGuid(reader.GetString(7), "ScenarioId"),
            ParseGuid(reader.GetString(8), "VersionId"),
            reader.GetInt32(9),
            reader.GetInt32(10),
            reader.GetString(11),
            reader.GetString(12),
            ParseTimestamp(reader.GetString(13), "ScenarioVersion.CreatedAt"));

        var status = ParseEnum<RunStatus>(reader.GetString(1), "RunStatus");
        var waitReason = reader.IsDBNull(2)
            ? null
            : ParseEnum<RunWaitReason>(reader.GetString(2), "RunWaitReason");

        var state = RunState.Restore(status, waitReason);
        var cursor = ExecutionCursorJson.Deserialize(reader.GetString(3));
        var variables = DeserializeVariables(reader.GetString(4));

        var run = AutomationRun.Restore(
            ParseGuid(reader.GetString(0), "RunId"),
            scenarioVersion,
            state,
            cursor,
            variables,
            ParseTimestamp(reader.GetString(5), "AutomationRun.CreatedAt"),
            ParseTimestamp(reader.GetString(6), "AutomationRun.UpdatedAt"));

        return new StoredAutomationRun(run, scenarioVersion);
    }

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
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
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
                $cursorJson,
                $variablesJson,
                $createdAt,
                $updatedAt)
            ON CONFLICT(RunId) DO UPDATE SET
                Status = excluded.Status,
                WaitReason = excluded.WaitReason,
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
        upsert.Parameters.AddWithValue("$cursorJson", ExecutionCursorJson.Serialize(run.Cursor));
        upsert.Parameters.AddWithValue("$variablesJson", SerializeVariables(run.Variables));
        upsert.Parameters.AddWithValue("$createdAt", FormatTimestamp(run.CreatedAt));
        upsert.Parameters.AddWithValue("$updatedAt", FormatTimestamp(run.UpdatedAt));

        await upsert.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string SerializeVariables(
        IReadOnlyDictionary<string, string> variables) =>
        JsonSerializer.Serialize(variables);

    private static Dictionary<string, string> DeserializeVariables(string json)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            ?? throw new InvalidOperationException("Stored run variables could not be deserialized.");

        return new Dictionary<string, string>(
            values,
            StringComparer.OrdinalIgnoreCase);
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
