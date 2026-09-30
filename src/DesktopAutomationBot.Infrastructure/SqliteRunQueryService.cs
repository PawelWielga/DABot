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
        if (!await RunsTableExistsAsync(
                connection,
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
        if (!await RunsTableExistsAsync(
                connection,
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
                    CreatedAt = DateTimeOffset.Parse(
                        reader.GetString(5),
                        System.Globalization.CultureInfo.InvariantCulture),
                    UpdatedAt = DateTimeOffset.Parse(
                        reader.GetString(6),
                        System.Globalization.CultureInfo.InvariantCulture),
                });
        }

        return items;
    }

    private static async Task<bool> RunsTableExistsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT 1
            FROM sqlite_master
            WHERE type = 'table'
              AND name = 'Runs'
            LIMIT 1;
            """;

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
