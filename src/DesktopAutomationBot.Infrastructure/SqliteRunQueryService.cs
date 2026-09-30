using System.Globalization;
using System.Text.Json;
using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using Microsoft.Data.Sqlite;

namespace DesktopAutomationBot.Infrastructure;

public sealed class SqliteRunQueryService(
    BotOptions options) : IRunQueryService
{
    public async Task<RunDashboardSummary> GetDashboardSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await OpenConnectionAsync(cancellationToken);
        if (!await TableExistsAsync(
                connection,
                "Runs",
                cancellationToken))
        {
            return new RunDashboardSummary();
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                COUNT(*) AS TotalRuns,
                SUM(CASE WHEN Status = 'Running' THEN 1 ELSE 0 END) AS RunningRuns,
                SUM(CASE WHEN Status = 'Waiting' THEN 1 ELSE 0 END) AS WaitingRuns,
                SUM(CASE WHEN Status = 'Failed' THEN 1 ELSE 0 END) AS FailedRuns,
                SUM(CASE WHEN Status = 'Completed' THEN 1 ELSE 0 END) AS CompletedRuns,
                SUM(CASE WHEN Status = 'Cancelled' THEN 1 ELSE 0 END) AS CancelledRuns
            FROM Runs;
            """;

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return new RunDashboardSummary();
        }

        return new RunDashboardSummary
        {
            TotalRuns = reader.GetInt32(0),
            RunningRuns = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
            WaitingRuns = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
            FailedRuns = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
            CompletedRuns = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
            CancelledRuns = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
        };
    }

    public async Task<IReadOnlyList<RunListItem>> ListRecentRunsAsync(
        int limit = 50,
        RunStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                limit,
                "Limit must be greater than zero.");
        }

        await using var connection =
            await OpenConnectionAsync(cancellationToken);
        if (!await TableExistsAsync(
                connection,
                "Runs",
                cancellationToken))
        {
            return [];
        }

        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                RunId,
                ScenarioId,
                ScenarioVersionId,
                Status,
                WaitReason,
                CreatedAt,
                UpdatedAt
            FROM Runs
            WHERE ($status IS NULL OR Status = $status)
            ORDER BY julianday(UpdatedAt) DESC, RunId DESC
            LIMIT $limit;
            """;

        command.Parameters.AddWithValue(
            "$status",
            status is null
                ? DBNull.Value
                : status.Value.ToString());
        command.Parameters.AddWithValue("$limit", limit);

        var items = new List<RunListItem>();

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(
                new RunListItem
                {
                    RunId = Guid.Parse(reader.GetString(0)),
                    ScenarioId = Guid.Parse(reader.GetString(1)),
                    ScenarioVersionId = Guid.Parse(reader.GetString(2)),
                    Status = Enum.Parse<RunStatus>(
                        reader.GetString(3),
                        ignoreCase: false),
                    WaitReason = reader.IsDBNull(4)
                        ? null
                        : Enum.Parse<RunWaitReason>(
                            reader.GetString(4),
                            ignoreCase: false),
                    CreatedAt = ParseTimestamp(reader.GetString(5)),
                    UpdatedAt = ParseTimestamp(reader.GetString(6)),
                });
        }

        return items;
    }

    public async Task<RunDetail?> GetRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException(
                "Run ID must not be empty.",
                nameof(runId));
        }

        await using var connection =
            await OpenConnectionAsync(cancellationToken);

        if (!await TableExistsAsync(
                connection,
                "Runs",
                cancellationToken) ||
            !await TableExistsAsync(
                connection,
                "ScenarioVersions",
                cancellationToken))
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                r.RunId,
                r.ScenarioId,
                r.ScenarioVersionId,
                r.Status,
                r.WaitReason,
                r.RetryNotBefore,
                r.VariablesJson,
                r.CreatedAt,
                r.UpdatedAt,
                sv.VersionNumber,
                sv.DefinitionJson
            FROM Runs r
            INNER JOIN ScenarioVersions sv
                ON sv.VersionId = r.ScenarioVersionId
            WHERE r.RunId = $runId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString("D"));

        RunDetail? detail;

        await using (var reader =
            await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            detail = new RunDetail
            {
                RunId = Guid.Parse(reader.GetString(0)),
                ScenarioId = Guid.Parse(reader.GetString(1)),
                ScenarioVersionId = Guid.Parse(reader.GetString(2)),
                Status = Enum.Parse<RunStatus>(
                    reader.GetString(3),
                    ignoreCase: false),
                WaitReason = reader.IsDBNull(4)
                    ? null
                    : Enum.Parse<RunWaitReason>(
                        reader.GetString(4),
                        ignoreCase: false),
                RetryNotBefore = reader.IsDBNull(5)
                    ? null
                    : ParseTimestamp(reader.GetString(5)),
                Variables = ReadVariables(reader.GetString(6)),
                CreatedAt = ParseTimestamp(reader.GetString(7)),
                UpdatedAt = ParseTimestamp(reader.GetString(8)),
                ScenarioVersionNumber = reader.GetInt32(9),
                ScenarioName = ReadScenarioName(reader.GetString(10)),
            };
        }

        if (await TableExistsAsync(
                connection,
                "StepAttempts",
                cancellationToken))
        {
            detail = detail with
            {
                StepAttempts = await LoadStepAttemptsAsync(
                    connection,
                    runId,
                    cancellationToken),
            };
        }

        if (await TableExistsAsync(
                connection,
                "AutomationEvents",
                cancellationToken) &&
            await TableExistsAsync(
                connection,
                "ResumeWorkItems",
                cancellationToken))
        {
            detail = detail with
            {
                Events = await LoadEventsAsync(
                    connection,
                    runId,
                    cancellationToken),
            };
        }

        return detail;
    }

    private static async Task<IReadOnlyList<RunEventItem>> LoadEventsAsync(
        SqliteConnection connection,
        Guid runId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                e.EventId,
                e.Type,
                e.CorrelationId,
                e.PayloadJson,
                e.OccurredAt,
                e.ReceivedAt,
                w.WorkItemId,
                w.Status,
                w.AttemptCount,
                w.NextAttemptAt,
                w.FinishedAt,
                w.ErrorMessage
            FROM ResumeWorkItems w
            INNER JOIN AutomationEvents e
                ON e.EventId = w.EventId
            WHERE w.RunId = $runId
            ORDER BY julianday(e.ReceivedAt), e.EventId;
            """;
        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString("D"));

        var items = new List<RunEventItem>();

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var payload = ScenarioVariableValue.ParseJson(
                reader.GetString(3));

            items.Add(
                new RunEventItem
                {
                    EventId = Guid.Parse(reader.GetString(0)),
                    Type = reader.GetString(1),
                    CorrelationId = reader.GetString(2),
                    PayloadKind = payload.Kind.ToString(),
                    Payload = FormatVariableValue(payload),
                    OccurredAt = ParseTimestamp(reader.GetString(4)),
                    ReceivedAt = ParseTimestamp(reader.GetString(5)),
                    WorkItemId = Guid.Parse(reader.GetString(6)),
                    WorkItemStatus = Enum.Parse<ResumeWorkItemStatus>(
                        reader.GetString(7),
                        ignoreCase: false),
                    AttemptCount = reader.GetInt32(8),
                    NextAttemptAt = reader.IsDBNull(9)
                        ? null
                        : ParseTimestamp(reader.GetString(9)),
                    FinishedAt = reader.IsDBNull(10)
                        ? null
                        : ParseTimestamp(reader.GetString(10)),
                    ErrorMessage = reader.IsDBNull(11)
                        ? null
                        : reader.GetString(11),
                });
        }

        return items;
    }

    private static async Task<IReadOnlyList<RunStepAttemptItem>> LoadStepAttemptsAsync(
        SqliteConnection connection,
        Guid runId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                AttemptId,
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
            ORDER BY julianday(StartedAt), AttemptNumber, AttemptId;
            """;
        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString("D"));

        var items = new List<RunStepAttemptItem>();

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(
                new RunStepAttemptItem
                {
                    AttemptId = Guid.Parse(reader.GetString(0)),
                    StepId = reader.GetString(1),
                    StepType = Enum.Parse<StepType>(
                        reader.GetString(2),
                        ignoreCase: false),
                    AttemptNumber = reader.GetInt32(3),
                    RetrySafety = Enum.Parse<StepRetrySafety>(
                        reader.GetString(4),
                        ignoreCase: false),
                    Status = Enum.Parse<StepAttemptStatus>(
                        reader.GetString(5),
                        ignoreCase: false),
                    StartedAt = ParseTimestamp(reader.GetString(6)),
                    UpdatedAt = ParseTimestamp(reader.GetString(7)),
                    FinishedAt = reader.IsDBNull(8)
                        ? null
                        : ParseTimestamp(reader.GetString(8)),
                    ErrorMessage = reader.IsDBNull(9)
                        ? null
                        : reader.GetString(9),
                });
        }

        return items;
    }

    private static IReadOnlyList<RunVariableItem> ReadVariables(
        string variablesJson)
    {
        var variables =
            JsonSerializer.Deserialize<
                Dictionary<string, ScenarioVariableValue>>(
                variablesJson) ??
            new Dictionary<string, ScenarioVariableValue>(
                StringComparer.OrdinalIgnoreCase);

        return variables
            .OrderBy(
                pair => pair.Key,
                StringComparer.OrdinalIgnoreCase)
            .Select(
                pair =>
                    new RunVariableItem(
                        pair.Key,
                        pair.Value.Kind.ToString(),
                        FormatVariableValue(pair.Value)))
            .ToArray();
    }

    private static string FormatVariableValue(
        ScenarioVariableValue value)
    {
        var element = value.ToJsonElement();

        return element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? string.Empty
            : element.GetRawText();
    }

    private static string ReadScenarioName(
        string definitionJson)
    {
        try
        {
            using var document =
                JsonDocument.Parse(definitionJson);

            if (document.RootElement.TryGetProperty(
                    "name",
                    out var name) &&
                name.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(name.GetString()))
            {
                return name.GetString()!;
            }
        }
        catch (JsonException)
        {
        }

        return "Scenario";
    }

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(
            value,
            CultureInfo.InvariantCulture);

    private static async Task<bool> TableExistsAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT 1
            FROM sqlite_master
            WHERE type = 'table'
              AND name = $tableName
            LIMIT 1;
            """;
        command.Parameters.AddWithValue(
            "$tableName",
            tableName);

        return await command.ExecuteScalarAsync(
            cancellationToken) is not null;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var databasePath = Path.GetFullPath(
            options.Storage.DatabasePath);
        var directory = Path.GetDirectoryName(databasePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                ForeignKeys = true,
            }.ToString());

        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
