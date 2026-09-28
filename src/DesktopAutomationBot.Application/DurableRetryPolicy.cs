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

        if (!StepRetryPolicy.HasRemainingAttempt(
                step,
                highestAttemptNumber))
        {
            throw new InvalidOperationException(
                $"Step '{step.Id}' exhausted its retry budget after " +
                $"{highestAttemptNumber} attempt(s).");
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
            GetHighestAttemptNumber(
                attempts,
                step.Id));
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
