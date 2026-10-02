using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class AdministrativeRunControlServiceTests
{
    [Fact]
    public async Task ResumeAsync_DelegatesAndWritesRequestedAndSucceededAudit()
    {
        var run = CreateRun();
        var runtime = new RecordingRunControl(run);
        var audit = new RecordingAdministrativeAuditSink();
        var service = new AdministrativeRunControlService(
            runtime,
            audit,
            TimeProvider.System);

        var result = await service.ResumeAsync(run.RunId);

        result.Run.Should().BeSameAs(run);
        runtime.ResumeCalls.Should().Be(1);
        runtime.CancelCalls.Should().Be(0);

        audit.Events.Should().HaveCount(2);
        audit.Events[0].Operation.Should()
            .Be(AdministrativeOperation.ResumeRun);
        audit.Events[0].Outcome.Should()
            .Be(AdministrativeAuditOutcome.Requested);
        audit.Events[0].RunId.Should().Be(run.RunId);

        audit.Events[1].OperationId.Should()
            .Be(audit.Events[0].OperationId);
        audit.Events[1].Outcome.Should()
            .Be(AdministrativeAuditOutcome.Succeeded);
        audit.Events[1].RunId.Should().Be(run.RunId);
    }

    [Fact]
    public async Task CancelAsync_DelegatesAndWritesRequestedAndSucceededAudit()
    {
        var run = CreateRun();
        var runtime = new RecordingRunControl(run);
        var audit = new RecordingAdministrativeAuditSink();
        var service = new AdministrativeRunControlService(
            runtime,
            audit,
            TimeProvider.System);

        var result = await service.CancelAsync(run.RunId);

        result.Should().BeSameAs(run);
        runtime.CancelCalls.Should().Be(1);
        runtime.ResumeCalls.Should().Be(0);

        audit.Events.Select(item => item.Outcome)
            .Should()
            .Equal(
                AdministrativeAuditOutcome.Requested,
                AdministrativeAuditOutcome.Succeeded);
        audit.Events.Should().OnlyContain(
            item =>
                item.Operation == AdministrativeOperation.CancelRun &&
                item.RunId == run.RunId);
    }

    [Fact]
    public async Task ResumeAsync_WhenRuntimeFails_AuditsFailureAndRethrows()
    {
        var run = CreateRun();
        var runtime = new RecordingRunControl(run)
        {
            ResumeException =
                new InvalidOperationException("cannot resume"),
        };
        var audit = new RecordingAdministrativeAuditSink();
        var service = new AdministrativeRunControlService(
            runtime,
            audit,
            TimeProvider.System);

        Func<Task> action = async () =>
            await service.ResumeAsync(run.RunId);

        await action.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("cannot resume");

        audit.Events.Should().HaveCount(2);
        audit.Events[1].Outcome.Should()
            .Be(AdministrativeAuditOutcome.Failed);
        audit.Events[1].FailureType.Should()
            .Be(nameof(InvalidOperationException));
    }

    private static AutomationRun CreateRun()
    {
        var now = DateTimeOffset.UtcNow;
        var version = ScenarioVersion.Capture(
            Guid.NewGuid(),
            1,
            new ScenarioDefinition
            {
                Name = "Administrative audit test",
                Steps =
                [
                    new ScenarioStep
                    {
                        Id = "open",
                        Type = StepType.OpenUrl,
                        Url = "https://example.test",
                    },
                ],
            },
            now);

        return AutomationRun.Create(
            version,
            now,
            Guid.NewGuid());
    }

    private sealed class RecordingRunControl(
        AutomationRun run) : IDurableRunControlService
    {
        public int ResumeCalls { get; private set; }

        public int CancelCalls { get; private set; }

        public Exception? ResumeException { get; init; }

        public Task<DurableScenarioExecutionResult> CloneAsync(
            Guid runId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DurableScenarioExecutionResult> ResumeManuallyAsync(
            Guid runId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ResumeCalls++;

            if (ResumeException is not null)
            {
                throw ResumeException;
            }

            return Task.FromResult(
                new DurableScenarioExecutionResult
                {
                    Run = run,
                    Outcome = DurableExecutionOutcome.Suspended,
                });
        }

        public Task<DurableScenarioExecutionResult> RetryNowAsync(
            Guid runId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AutomationRun> CancelAsync(
            Guid runId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CancelCalls++;
            return Task.FromResult(run);
        }
    }

    private sealed class RecordingAdministrativeAuditSink :
        IAdministrativeAuditSink
    {
        public List<AdministrativeAuditEvent> Events { get; } = [];

        public Task WriteAsync(
            AdministrativeAuditEvent auditEvent,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }
}
