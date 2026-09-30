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

public sealed record RunVariableItem(
    string Name,
    string Kind,
    string Value);

public sealed record RunStepAttemptItem
{
    public required Guid AttemptId { get; init; }

    public required string StepId { get; init; }

    public required StepType StepType { get; init; }

    public required int AttemptNumber { get; init; }

    public required StepRetrySafety RetrySafety { get; init; }

    public required StepAttemptStatus Status { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }

    public string? ErrorMessage { get; init; }
}

public sealed record RunDetail
{
    public required Guid RunId { get; init; }

    public required Guid ScenarioId { get; init; }

    public required Guid ScenarioVersionId { get; init; }

    public required int ScenarioVersionNumber { get; init; }

    public required string ScenarioName { get; init; }

    public required RunStatus Status { get; init; }

    public RunWaitReason? WaitReason { get; init; }

    public DateTimeOffset? RetryNotBefore { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public IReadOnlyList<RunVariableItem> Variables { get; init; } = [];

    public IReadOnlyList<RunStepAttemptItem> StepAttempts { get; init; } = [];
}

public interface IRunQueryService
{
    Task<RunDashboardSummary> GetDashboardSummaryAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RunListItem>> ListRecentRunsAsync(
        int limit = 50,
        RunStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<RunDetail?> GetRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
}
