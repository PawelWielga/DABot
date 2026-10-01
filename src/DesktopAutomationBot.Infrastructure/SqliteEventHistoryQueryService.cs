using System.Globalization;
using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using Microsoft.Data.Sqlite;

namespace DesktopAutomationBot.Infrastructure;

public sealed class SqliteEventHistoryQueryService(
    BotOptions options) : IEventHistoryQueryService
{
    public async Task<IReadOnlyList<EventHistoryItem>> ListRecentAsync(
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

        await using var connection =
            await OpenConnectionAsync(cancellationToken);

        if (!await TableExistsAsync(
                connection,
                "AutomationEvents",
                cancellationToken) ||
            !await TableExistsAsync(
                connection,
                "ResumeWorkItems",
                cancellationToken))
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                e.EventId,
                e.Type,
                e.CorrelationId,
                e.OccurredAt,
                e.ReceivedAt,
                w.RunId,
                w.WorkItemId,
                w.Status,
                w.AttemptCount,
                w.NextAttemptAt,
                w.FinishedAt,
                w.ErrorMessage
            FROM AutomationEvents e
            LEFT JOIN ResumeWorkItems w
                ON w.EventId = e.EventId
            ORDER BY julianday(e.ReceivedAt) DESC, e.EventId DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var items = new List<EventHistoryItem>();

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(
                new EventHistoryItem
                {
                    EventId = Guid.Parse(reader.GetString(0)),
                    Type = reader.GetString(1),
                    CorrelationId = reader.GetString(2),
                    OccurredAt = ParseTimestamp(reader.GetString(3)),
                    ReceivedAt = ParseTimestamp(reader.GetString(4)),
                    RunId = reader.IsDBNull(5)
                        ? null
                        : Guid.Parse(reader.GetString(5)),
                    WorkItemId = reader.IsDBNull(6)
                        ? null
                        : Guid.Parse(reader.GetString(6)),
                    WorkItemStatus = reader.IsDBNull(7)
                        ? null
                        : Enum.Parse<ResumeWorkItemStatus>(
                            reader.GetString(7),
                            ignoreCase: false),
                    AttemptCount = reader.IsDBNull(8)
                        ? null
                        : reader.GetInt32(8),
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
