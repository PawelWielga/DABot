using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Core.Tests;

public sealed class RunStateTests
{
    [Fact]
    public void CreateQueued_ReturnsNonTerminalQueuedState()
    {
        var state = RunState.CreateQueued();

        state.Status.Should().Be(RunStatus.Queued);
        state.WaitReason.Should().BeNull();
        state.IsTerminal.Should().BeFalse();
    }

    [Fact]
    public void NormalLifecycle_ClearsWaitReasonWhenResumed()
    {
        var state = RunState.CreateQueued()
            .Start()
            .Wait(RunWaitReason.Event);

        state.Status.Should().Be(RunStatus.Waiting);
        state.WaitReason.Should().Be(RunWaitReason.Event);

        state = state.Resume();

        state.Status.Should().Be(RunStatus.Running);
        state.WaitReason.Should().BeNull();

        state = state.Complete();

        state.Status.Should().Be(RunStatus.Completed);
        state.WaitReason.Should().BeNull();
        state.IsTerminal.Should().BeTrue();
    }

    [Theory]
    [InlineData(RunWaitReason.Event)]
    [InlineData(RunWaitReason.Human)]
    [InlineData(RunWaitReason.Retry)]
    [InlineData(RunWaitReason.Schedule)]
    public void Wait_SupportsEveryDefinedWaitReason(RunWaitReason reason)
    {
        var state = RunState.CreateQueued()
            .Start()
            .Wait(reason);

        state.Status.Should().Be(RunStatus.Waiting);
        state.WaitReason.Should().Be(reason);
    }

    [Theory]
    [InlineData(RunStatus.Queued, RunStatus.Running)]
    [InlineData(RunStatus.Queued, RunStatus.Cancelled)]
    [InlineData(RunStatus.Running, RunStatus.Waiting)]
    [InlineData(RunStatus.Running, RunStatus.Completed)]
    [InlineData(RunStatus.Running, RunStatus.Failed)]
    [InlineData(RunStatus.Running, RunStatus.Cancelled)]
    [InlineData(RunStatus.Waiting, RunStatus.Running)]
    [InlineData(RunStatus.Waiting, RunStatus.Failed)]
    [InlineData(RunStatus.Waiting, RunStatus.Cancelled)]
    public void CanTransition_ForAllowedTransition_ReturnsTrue(
        RunStatus currentStatus,
        RunStatus nextStatus)
    {
        RunStateTransitionPolicy.CanTransition(currentStatus, nextStatus)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(RunStatus.Queued, RunStatus.Waiting)]
    [InlineData(RunStatus.Queued, RunStatus.Completed)]
    [InlineData(RunStatus.Queued, RunStatus.Failed)]
    [InlineData(RunStatus.Running, RunStatus.Running)]
    [InlineData(RunStatus.Waiting, RunStatus.Waiting)]
    [InlineData(RunStatus.Waiting, RunStatus.Completed)]
    [InlineData(RunStatus.Completed, RunStatus.Running)]
    [InlineData(RunStatus.Completed, RunStatus.Cancelled)]
    [InlineData(RunStatus.Failed, RunStatus.Running)]
    [InlineData(RunStatus.Failed, RunStatus.Cancelled)]
    [InlineData(RunStatus.Cancelled, RunStatus.Running)]
    public void CanTransition_ForForbiddenTransition_ReturnsFalse(
        RunStatus currentStatus,
        RunStatus nextStatus)
    {
        RunStateTransitionPolicy.CanTransition(currentStatus, nextStatus)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(RunStatus.Completed)]
    [InlineData(RunStatus.Failed)]
    [InlineData(RunStatus.Cancelled)]
    public void TerminalState_RejectsFurtherTransitions(RunStatus terminalStatus)
    {
        var state = RunState.Restore(terminalStatus);

        var act = () => state.TransitionTo(RunStatus.Running);

        act.Should().Throw<InvalidOperationException>();
        state.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void Restore_WhenWaitingWithoutReason_Throws()
    {
        var act = () => RunState.Restore(RunStatus.Waiting);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*requires a wait reason*");
    }

    [Fact]
    public void Restore_WhenNonWaitingHasReason_Throws()
    {
        var act = () => RunState.Restore(
            RunStatus.Running,
            RunWaitReason.Event);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*must not have a wait reason*");
    }

    [Fact]
    public void TransitionToWaiting_WithoutReason_Throws()
    {
        var state = RunState.CreateQueued().Start();

        var act = () => state.TransitionTo(RunStatus.Waiting);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*requires a wait reason*");
    }

    [Fact]
    public void WaitingState_CanFailWithoutResuming()
    {
        var state = RunState.CreateQueued()
            .Start()
            .Wait(RunWaitReason.Retry)
            .Fail();

        state.Status.Should().Be(RunStatus.Failed);
        state.WaitReason.Should().BeNull();
        state.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void QueuedState_CanBeCancelledBeforeExecutionStarts()
    {
        var state = RunState.CreateQueued().Cancel();

        state.Status.Should().Be(RunStatus.Cancelled);
        state.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void QueuedState_CannotCompleteWithoutRunning()
    {
        var state = RunState.CreateQueued();

        var act = () => state.Complete();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Queued*Completed*");
    }

    [Fact]
    public void WaitingState_CannotChangeWaitReasonWithoutResuming()
    {
        var state = RunState.CreateQueued()
            .Start()
            .Wait(RunWaitReason.Event);

        var act = () => state.Wait(RunWaitReason.Human);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Waiting*Waiting*");
    }

    [Fact]
    public void Restore_WithUnknownStatus_Throws()
    {
        var act = () => RunState.Restore((RunStatus)999);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Restore_WithUnknownWaitReason_Throws()
    {
        var act = () => RunState.Restore(
            RunStatus.Waiting,
            (RunWaitReason)999);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
