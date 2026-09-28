using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public interface IStepAttemptStore
{
    Task SaveStepAttemptAsync(
        StepAttempt attempt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StepAttempt>> LoadStepAttemptsAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StepAttempt>> MarkStartedAttemptsUnknownAsync(
        Guid runId,
        DateTimeOffset detectedAt,
        CancellationToken cancellationToken = default);
}
