namespace DesktopAutomationBot.Core;

public enum RunStatus
{
    Queued,
    Running,
    Waiting,
    Completed,
    Failed,
    Cancelled,
}

public enum RunWaitReason
{
    Event,
    Human,
    Retry,
    Schedule,
}

public sealed record RunState
{
    private RunState(RunStatus status, RunWaitReason? waitReason)
    {
        Status = status;
        WaitReason = waitReason;
    }

    public RunStatus Status { get; }

    public RunWaitReason? WaitReason { get; }

    public bool IsTerminal =>
        Status is RunStatus.Completed or RunStatus.Failed or RunStatus.Cancelled;

    public static RunState CreateQueued() =>
        new(RunStatus.Queued, waitReason: null);

    public static RunState Restore(
        RunStatus status,
        RunWaitReason? waitReason = null)
    {
        ValidateShape(status, waitReason);
        return new RunState(status, waitReason);
    }

    public RunState Start() =>
        TransitionTo(RunStatus.Running);

    public RunState Wait(RunWaitReason reason) =>
        TransitionTo(RunStatus.Waiting, reason);

    public RunState Resume() =>
        TransitionTo(RunStatus.Running);

    public RunState Complete() =>
        TransitionTo(RunStatus.Completed);

    public RunState Fail() =>
        TransitionTo(RunStatus.Failed);

    public RunState Cancel() =>
        TransitionTo(RunStatus.Cancelled);

    public RunState TransitionTo(
        RunStatus nextStatus,
        RunWaitReason? waitReason = null)
    {
        ValidateShape(nextStatus, waitReason);

        if (!RunStateTransitionPolicy.CanTransition(Status, nextStatus))
        {
            throw new InvalidOperationException(
                $"Run state cannot transition from '{Status}' to '{nextStatus}'.");
        }

        return new RunState(nextStatus, waitReason);
    }

    private static void ValidateShape(
        RunStatus status,
        RunWaitReason? waitReason)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unknown run status.");
        }

        if (waitReason is not null && !Enum.IsDefined(waitReason.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(waitReason),
                waitReason,
                "Unknown run wait reason.");
        }

        if (status == RunStatus.Waiting && waitReason is null)
        {
            throw new ArgumentException(
                "Waiting run state requires a wait reason.",
                nameof(waitReason));
        }

        if (status != RunStatus.Waiting && waitReason is not null)
        {
            throw new ArgumentException(
                $"Run state '{status}' must not have a wait reason.",
                nameof(waitReason));
        }
    }
}

public static class RunStateTransitionPolicy
{
    public static bool CanTransition(
        RunStatus currentStatus,
        RunStatus nextStatus)
    {
        if (!Enum.IsDefined(currentStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentStatus),
                currentStatus,
                "Unknown current run status.");
        }

        if (!Enum.IsDefined(nextStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(nextStatus),
                nextStatus,
                "Unknown next run status.");
        }

        return currentStatus switch
        {
            RunStatus.Queued =>
                nextStatus is RunStatus.Running or RunStatus.Cancelled,

            RunStatus.Running =>
                nextStatus is
                    RunStatus.Waiting or
                    RunStatus.Completed or
                    RunStatus.Failed or
                    RunStatus.Cancelled,

            RunStatus.Waiting =>
                nextStatus is
                    RunStatus.Running or
                    RunStatus.Failed or
                    RunStatus.Cancelled,

            RunStatus.Completed or
            RunStatus.Failed or
            RunStatus.Cancelled => false,

            _ => false,
        };
    }
}
