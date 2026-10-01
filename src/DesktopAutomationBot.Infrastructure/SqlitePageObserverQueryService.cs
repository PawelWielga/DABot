using System.Globalization;
using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using Microsoft.Data.Sqlite;

namespace DesktopAutomationBot.Infrastructure;

public sealed class SqlitePageObserverQueryService(
    BotOptions options) : IPageObserverQueryService
{
    public async Task<IReadOnlyList<PageObserverListItem>> ListAsync(
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

        await using var connection = await OpenConnectionAsync(cancellationToken);

        if (!await TableExistsAsync(connection, "PageObservers", cancellationToken) ||
            !await TableExistsAsync(connection, "PageObserverSnapshots", cancellationToken))
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                o.ObserverId,
                o.Name,
                o.Url,
                o.BrowserProfile,
                o.Condition,
                o.EventType,
                o.CorrelationId,
                o.PollIntervalMs,
                o.Enabled,
                s.LastCheckedAt,
                s.NextCheckAt,
                s.LastEventAt,
                s.FailureCount,
                s.LastError
            FROM PageObservers o
            INNER JOIN PageObserverSnapshots s
                ON s.ObserverId = o.ObserverId
            ORDER BY
                o.Enabled DESC,
                o.Name COLLATE NOCASE,
                o.ObserverId
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var items = new List<PageObserverListItem>();

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(
                new PageObserverListItem
                {
                    ObserverId = Guid.Parse(reader.GetString(0)),
                    Name = reader.GetString(1),
                    Url = reader.GetString(2),
                    BrowserProfile = reader.IsDBNull(3)
                        ? null
                        : reader.GetString(3),
                    Condition = Enum.Parse<PageObserverConditionKind>(
                        reader.GetString(4),
                        ignoreCase: false),
                    EventType = reader.GetString(5),
                    CorrelationId = reader.GetString(6),
                    PollIntervalMs = reader.GetInt32(7),
                    Enabled = reader.GetInt32(8) != 0,
                    LastCheckedAt = ReadTimestamp(reader, 9),
                    NextCheckAt = ReadTimestamp(reader, 10),
                    LastEventAt = ReadTimestamp(reader, 11),
                    FailureCount = reader.GetInt32(12),
                    LastError = reader.IsDBNull(13)
                        ? null
                        : reader.GetString(13),
                });
        }

        return items;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var databasePath = Path.GetFullPath(options.Storage.DatabasePath);
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
        command.Parameters.AddWithValue("$tableName", tableName);

        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static DateTimeOffset? ReadTimestamp(
        SqliteDataReader reader,
        int ordinal) =>
        reader.IsDBNull(ordinal)
            ? null
            : DateTimeOffset.Parse(
                reader.GetString(ordinal),
                CultureInfo.InvariantCulture);
}
