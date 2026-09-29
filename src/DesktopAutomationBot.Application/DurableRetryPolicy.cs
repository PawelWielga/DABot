using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

internal static class DurableRetryPolicy
{
    public static int GetHighestAttemptNumber(
        IEnumerable<StepAttempt> attempts,
        string stepId)
    {
        ArgumentNullException.ThrowIfNull(attempts);
        ArgumentException.ThrowIfNullOrWhiteSpace(stepId);

        return attempts
            .Where(
                attempt => string.Equals(
                    attempt.StepId,
                    stepId,
                    StringComparison.OrdinalIgnoreCase))
            .Select(attempt => attempt.AttemptNumber)
            .DefaultIfEmpty(0)
            .Max();
    }

    public static int GetNextAttemptNumber(
        ScenarioStep step,
        IEnumerable<StepAttempt> attempts)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentException.ThrowIfNullOrWhiteSpace(step.Id);

        var highestAttemptNumber = GetHighestAttemptNumber(
            attempts,
            step.Id);

        var currentOccurrenceAttemptCount =
            GetCurrentOccurrenceAttemptCount(attempts, step.Id);

        if (!StepRetryPolicy.HasRemainingAttempt(
                step,
                currentOccurrenceAttemptCount))
        {
            throw new InvalidOperationException(
                $"Step '{step.Id}' exhausted its retry budget after " +
                $"{currentOccurrenceAttemptCount} attempt(s) in the current execution.");
        }

        return checked(highestAttemptNumber + 1);
    }

    public static bool HasRemainingAttempt(
        ScenarioStep step,
        IEnumerable<StepAttempt> attempts)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentException.ThrowIfNullOrWhiteSpace(step.Id);

        return StepRetryPolicy.HasRemainingAttempt(
            step,
            GetCurrentOccurrenceAttemptCount(
                attempts,
                step.Id));
    }

    private static int GetCurrentOccurrenceAttemptCount(
        IEnumerable<StepAttempt> attempts,
        string stepId)
    {
        var matching = attempts
            .Where(attempt => string.Equals(
                attempt.StepId,
                stepId,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var lastCompletedAttemptNumber = matching
            .Where(attempt => attempt.Status == StepAttemptStatus.Completed)
            .Select(attempt => attempt.AttemptNumber)
            .DefaultIfEmpty(0)
            .Max();

        return matching
            .Where(attempt => attempt.AttemptNumber > lastCompletedAttemptNumber)
            .Select(attempt => attempt.AttemptNumber)
            .Distinct()
            .Count();
    }

    public static DateTimeOffset GetRetryNotBefore(
        ScenarioStep step,
        DateTimeOffset anchor) =>
        anchor + StepRetryPolicy.GetRetryDelay(step);

    public static StepRecoveryAction GetRecoveryAction(
        ScenarioStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        return StepAttemptRecoveryPolicy.Decide(
            StepRetrySafetyClassifier.Resolve(step));
    }
}
