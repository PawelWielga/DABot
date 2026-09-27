namespace DesktopAutomationBot.Core;

public enum StepAttemptStatus
{
    Started,
    Completed,
    Failed,
    Unknown,
}

public enum StepRetrySafety
{
    SafeToRetry,
    Idempotent,
    NeedsVerification,
    NeverRetryAutomatically,
}

public enum StepRecoveryAction
{
    None,
    RetryAutomatically,
    VerifyBeforeRetry,
    WaitingForHuman,
}

public sealed record StepAttempt
{
    public required Guid AttemptId { get; init; }

    public required Guid RunId { get; init; }

    public required string StepId { get; init; }

    public required StepType StepType { get; init; }

    public required int AttemptNumber { get; init; }

    public required StepRetrySafety RetrySafety { get; init; }

    public required StepAttemptStatus Status { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }

    public string? ErrorMessage { get; init; }

    public static StepAttempt Start(
        Guid runId,
        ScenarioStep step,
        int attemptNumber,
        DateTimeOffset startedAt)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("Run ID must not be empty.", nameof(runId));
        }

        ArgumentNullException.ThrowIfNull(step);

        if (string.IsNullOrWhiteSpace(step.Id))
        {
            throw new ArgumentException(
                "Step must have a stable ID before an attempt can be created.",
                nameof(step));
        }

        if (attemptNumber < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attemptNumber),
                attemptNumber,
                "Attempt number must be at least 1.");
        }

        return new StepAttempt
        {
            AttemptId = Guid.NewGuid(),
            RunId = runId,
            StepId = step.Id.Trim(),
            StepType = step.Type,
            AttemptNumber = attemptNumber,
            RetrySafety = StepRetrySafetyClassifier.Resolve(step),
            Status = StepAttemptStatus.Started,
            StartedAt = startedAt,
            UpdatedAt = startedAt,
        };
    }

    public StepAttempt MarkCompleted(DateTimeOffset completedAt)
    {
        EnsureStarted();

        return this with
        {
            Status = StepAttemptStatus.Completed,
            UpdatedAt = completedAt,
            FinishedAt = completedAt,
            ErrorMessage = null,
        };
    }

    public StepAttempt MarkFailed(string errorMessage, DateTimeOffset failedAt)
    {
        EnsureStarted();
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return this with
        {
            Status = StepAttemptStatus.Failed,
            UpdatedAt = failedAt,
            FinishedAt = failedAt,
            ErrorMessage = errorMessage,
        };
    }

    public StepAttempt MarkUnknown(DateTimeOffset detectedAt)
    {
        EnsureStarted();

        return this with
        {
            Status = StepAttemptStatus.Unknown,
            UpdatedAt = detectedAt,
            FinishedAt = null,
            ErrorMessage = null,
        };
    }

    private void EnsureStarted()
    {
        if (Status != StepAttemptStatus.Started)
        {
            throw new InvalidOperationException(
                $"Step attempt '{AttemptId}' cannot transition from '{Status}'.");
        }
    }
}

public static class StepRetrySafetyClassifier
{
    public static StepRetrySafety Resolve(ScenarioStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        return step.RetrySafety ?? GetDefault(step.Type);
    }

    public static StepRetrySafety GetDefault(StepType stepType) =>
        stepType switch
        {
            StepType.ReadText => StepRetrySafety.SafeToRetry,
            StepType.WaitFor => StepRetrySafety.SafeToRetry,
            StepType.Screenshot => StepRetrySafety.SafeToRetry,
            StepType.Delay => StepRetrySafety.SafeToRetry,
            StepType.If => StepRetrySafety.SafeToRetry,
            StepType.Loop => StepRetrySafety.SafeToRetry,

            StepType.OpenUrl => StepRetrySafety.Idempotent,
            StepType.FillText => StepRetrySafety.Idempotent,
            StepType.PasteText => StepRetrySafety.Idempotent,

            StepType.Click => StepRetrySafety.NeedsVerification,

            StepType.CallApi => StepRetrySafety.NeverRetryAutomatically,

            _ => StepRetrySafety.NeverRetryAutomatically,
        };
}

public static class StepAttemptRecoveryPolicy
{
    public static StepRecoveryAction Decide(StepAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        if (attempt.Status is StepAttemptStatus.Completed or StepAttemptStatus.Failed)
        {
            return StepRecoveryAction.None;
        }

        return attempt.RetrySafety switch
        {
            StepRetrySafety.SafeToRetry => StepRecoveryAction.RetryAutomatically,
            StepRetrySafety.Idempotent => StepRecoveryAction.RetryAutomatically,
            StepRetrySafety.NeedsVerification => StepRecoveryAction.VerifyBeforeRetry,
            StepRetrySafety.NeverRetryAutomatically => StepRecoveryAction.WaitingForHuman,
            _ => StepRecoveryAction.WaitingForHuman,
        };
    }
}
