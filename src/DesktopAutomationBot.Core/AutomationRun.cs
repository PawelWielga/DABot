using System.Collections.ObjectModel;

namespace DesktopAutomationBot.Core;

public sealed class AutomationRun
{
    private AutomationRun(
        Guid runId,
        Guid scenarioId,
        Guid scenarioVersionId,
        RunState state,
        ExecutionCursor cursor,
        IReadOnlyDictionary<string, string> variables,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? retryNotBefore)
    {
        RunId = runId;
        ScenarioId = scenarioId;
        ScenarioVersionId = scenarioVersionId;
        State = state;
        Cursor = cursor;
        Variables = variables;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        RetryNotBefore = retryNotBefore;
    }

    public Guid RunId { get; }

    public Guid ScenarioId { get; }

    public Guid ScenarioVersionId { get; }

    public RunState State { get; }

    public ExecutionCursor Cursor { get; }

    public IReadOnlyDictionary<string, string> Variables { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; }

    public DateTimeOffset? RetryNotBefore { get; }

    public static AutomationRun Create(
        ScenarioVersion scenarioVersion,
        DateTimeOffset createdAt,
        Guid? runId = null,
        ExecutionCursor? cursor = null,
        IReadOnlyDictionary<string, string>? variables = null)
    {
        ArgumentNullException.ThrowIfNull(scenarioVersion);

        var resolvedRunId = ResolveRunId(runId);
        var definition = scenarioVersion.MaterializeDefinition();
        var resolvedCursor = cursor ?? ExecutionCursor.Start(definition);

        ValidateCursor(definition, resolvedCursor);

        if (resolvedCursor.IsCompleted)
        {
            throw new ArgumentException(
                "A new queued run cannot start with a completed execution cursor.",
                nameof(cursor));
        }

        return new AutomationRun(
            resolvedRunId,
            scenarioVersion.ScenarioId,
            scenarioVersion.VersionId,
            RunState.CreateQueued(),
            CopyCursor(resolvedCursor),
            CopyVariables(variables),
            createdAt,
            createdAt,
            retryNotBefore: null);
    }

    public static AutomationRun Restore(
        Guid runId,
        ScenarioVersion scenarioVersion,
        RunState state,
        ExecutionCursor cursor,
        IReadOnlyDictionary<string, string>? variables,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? retryNotBefore = null)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("Run ID must not be empty.", nameof(runId));
        }

        ArgumentNullException.ThrowIfNull(scenarioVersion);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(cursor);

        if (updatedAt < createdAt)
        {
            throw new ArgumentException(
                "Run updated timestamp must not be earlier than its creation timestamp.",
                nameof(updatedAt));
        }

        ValidateRetrySchedule(state, retryNotBefore);

        var definition = scenarioVersion.MaterializeDefinition();
        ValidateCursor(definition, cursor);

        if (state.Status == RunStatus.Completed && !cursor.IsCompleted)
        {
            throw new ArgumentException(
                "A completed run must have a completed execution cursor.",
                nameof(cursor));
        }

        return new AutomationRun(
            runId,
            scenarioVersion.ScenarioId,
            scenarioVersion.VersionId,
            state,
            CopyCursor(cursor),
            CopyVariables(variables),
            createdAt,
            updatedAt,
            retryNotBefore);
    }

    private static Guid ResolveRunId(Guid? runId)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("Run ID must not be empty.", nameof(runId));
        }

        return runId ?? Guid.NewGuid();
    }

    private static void ValidateRetrySchedule(
        RunState state,
        DateTimeOffset? retryNotBefore)
    {
        var isRetryWait =
            state.Status == RunStatus.Waiting &&
            state.WaitReason == RunWaitReason.Retry;

        if (!isRetryWait && retryNotBefore is not null)
        {
            throw new ArgumentException(
                "RetryNotBefore can only be set for a run waiting with reason Retry.",
                nameof(retryNotBefore));
        }
    }

    private static void ValidateCursor(
        ScenarioDefinition definition,
        ExecutionCursor cursor)
    {
        var validation = new ExecutionCursorValidator().Validate(definition, cursor);
        if (validation.IsValid)
        {
            return;
        }

        throw new ArgumentException(
            "Execution cursor is not valid for the scenario version: " +
            string.Join(" ", validation.Errors),
            nameof(cursor));
    }

    private static ExecutionCursor CopyCursor(ExecutionCursor cursor) =>
        ExecutionCursorJson.Deserialize(ExecutionCursorJson.Serialize(cursor));

    private static IReadOnlyDictionary<string, string> CopyVariables(
        IReadOnlyDictionary<string, string>? variables)
    {
        var copy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (variables is not null)
        {
            foreach (var (name, value) in variables)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(name);

                if (value is null)
                {
                    throw new ArgumentException(
                        $"Run variable '{name}' must not have a null value.",
                        nameof(variables));
                }

                copy.Add(name, value);
            }
        }

        return new ReadOnlyDictionary<string, string>(copy);
    }
}
