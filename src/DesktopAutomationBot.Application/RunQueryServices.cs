using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public sealed record RunDashboardSummary
{
    public int TotalRuns { get; init; }

    public int RunningRuns { get; init; }

    public int WaitingRuns { get; init; }

    public int FailedRuns { get; init; }

    public int CompletedRuns { get; init; }

    public int CancelledRuns { get; init; }
}

public sealed record RunListItem
{
    public required Guid RunId { get; init; }

    public required Guid ScenarioId { get; init; }

    public required Guid ScenarioVersionId { get; init; }

    public required RunStatus Status { get; init; }

    public RunWaitReason? WaitReason { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}

public interface IRunQueryService
{
    Task<RunDashboardSummary> GetDashboardSummaryAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RunListItem>> ListRecentRunsAsync(
        int limit = 50,
        RunStatus? status = null,
        CancellationToken cancellationToken = default);
}
