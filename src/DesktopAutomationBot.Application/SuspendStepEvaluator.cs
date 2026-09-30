using System.Text.Json;
using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

internal static class SuspendStepEvaluator
{
    public static RunWaitReason GetWaitReason(ScenarioStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        if (step.Type != StepType.Suspend)
        {
            throw new ArgumentException(
                "Suspend wait reason can only be evaluated for Suspend steps.",
                nameof(step));
        }

        if (step.Parameters is null ||
            !step.Parameters.TryGetValue("reason", out var rawReason))
        {
            return RunWaitReason.Human;
        }

        if (rawReason.ValueKind != JsonValueKind.String ||
            !Enum.TryParse<RunWaitReason>(
                rawReason.GetString(),
                ignoreCase: true,
                out var reason) ||
            reason == RunWaitReason.Retry)
        {
            throw new InvalidOperationException(
                "Suspend reason must be Human, Event, or Schedule.");
        }

        return reason;
    }

    public static EventWaitRegistration CreateEventWait(
        ScenarioStep step,
        Guid runId,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(step);

        if (GetWaitReason(step) != RunWaitReason.Event)
        {
            throw new ArgumentException(
                "Event wait metadata is only valid for Suspend steps waiting for Event.",
                nameof(step));
        }

        if (step.Parameters is null ||
            !step.Parameters.TryGetValue("correlationId", out var rawCorrelation) ||
            rawCorrelation.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(rawCorrelation.GetString()))
        {
            throw new InvalidOperationException(
                "Suspend with reason Event requires parameters.correlationId.");
        }

        string? eventType = null;
        if (step.Parameters.TryGetValue("eventType", out var rawEventType))
        {
            if (rawEventType.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(rawEventType.GetString()))
            {
                throw new InvalidOperationException(
                    "Suspend parameters.eventType must be a non-empty string when specified.");
            }

            eventType = rawEventType.GetString()!.Trim();
        }

        return new EventWaitRegistration
        {
            RunId = runId,
            CorrelationId = rawCorrelation.GetString()!.Trim(),
            EventType = eventType,
            CreatedAt = createdAt,
        };
    }
}
