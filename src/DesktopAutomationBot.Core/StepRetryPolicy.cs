namespace DesktopAutomationBot.Core;

public static class StepRetryPolicy
{
    public static int GetMaximumAttempts(ScenarioStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        var retryCount = step.RetryCount ?? 0;

        if (retryCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(step),
                retryCount,
                "Retry count must not be negative.");
        }

        if (retryCount == int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(step),
                retryCount,
                $"Retry count must be at most {int.MaxValue - 1}.");
        }

        return retryCount + 1;
    }

    public static TimeSpan GetRetryDelay(ScenarioStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        var retryDelayMs = step.RetryDelayMs ?? 0;

        if (retryDelayMs < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(step),
                retryDelayMs,
                "Retry delay must not be negative.");
        }

        return TimeSpan.FromMilliseconds(retryDelayMs);
    }

    public static bool HasRemainingAttempt(
        ScenarioStep step,
        int highestAttemptNumber)
    {
        if (highestAttemptNumber < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(highestAttemptNumber),
                highestAttemptNumber,
                "Highest attempt number must not be negative.");
        }

        return highestAttemptNumber < GetMaximumAttempts(step);
    }
}
