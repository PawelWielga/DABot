# Durable workflows and web management architecture

## Purpose

This document describes the target execution model for DABot beyond a single synchronous scenario run.

The central rule is simple:

> Browser steps stay generic. Long-running waiting is represented by persisted state and events, not by keeping the scenario executor blocked.

## Components

```text
CLI ---------+
             |
Web ---------+----> Application
             |          |
API ---------+          +---- ScenarioExecutor
                        +---- RunCoordinator
                        +---- EventCoordinator
                                  |
                    +-------------+-------------+
                    |                           |
                 RunStore                  Event transport
                    |                           |
                 SQLite                    adapter(s)
                    |
                Worker queue
                    |
             Browser worker
                    |
                Playwright
```

## Scenario versus run

A scenario is a reusable definition.

A run is one concrete execution of a scenario.

A scenario can be immutable or versioned. Runtime state must never be stored by mutating the scenario definition.

Suggested run fields:

```text
RunId
ScenarioId / ScenarioVersion
Status
ExecutionCursor
Variables
WaitingForEvent
CorrelationId
CreatedAt
UpdatedAt
Attempt
ErrorCount
LeaseOwner
LeaseUntil
```

## ScenarioRunRequest

The durable execution entry contract is now represented by `ScenarioRunRequest`.

It intentionally requires an immutable `ScenarioVersion`, rather than a mutable `ScenarioDefinition`:

```csharp
public sealed record ScenarioRunRequest
{
    public required ScenarioVersion ScenarioVersion { get; init; }
    public Guid? RunId { get; init; }
    public ExecutionCursor? Cursor { get; init; }
    public Dictionary<string, string> Variables { get; init; } = [];
}
```

`RunId` is externally controllable. When omitted, a new GUID is generated.

`CreateRun` produces an `AutomationRun` bound permanently to the supplied `ScenarioVersion.VersionId`. `DurableScenarioExecutor` executes this version-bound path while the original synchronous executor continues to accept `ScenarioDefinition` for backward compatibility.



## DurableScenarioExecutor

`DurableScenarioExecutor` is the first Application-level orchestration path that executes a `ScenarioRunRequest` rather than a mutable scenario definition.

For a new durable run it:

1. persists the initial `Queued` run,
2. transitions and persists it as `Running`,
3. opens the browser,
4. resolves the step named by the persisted `ExecutionCursor`,
5. persists a `StepAttempt` in `Started` state,
6. invokes the existing generic `IStepHandler`,
7. persists the attempt as `Completed`, `Failed`, or `Unknown`,
8. advances and persists the cursor and variables,
9. completes, fails, or cancels the run.

The `Started` write happens before the handler is invoked. After a handler returns successfully, attempt finalization and the updated run snapshot are written with a non-cancelled persistence token so a caller cancellation cannot interrupt the durability boundary after a side effect has already completed.

Cancellation while a handler is executing conservatively marks the attempt `Unknown`, because the process cannot prove whether the browser-side effect occurred before cancellation was observed. The run then transitions to `Cancelled`.

Durable execution uses the stable GUID `RunId` as the execution-context artifact directory name. This starts moving screenshots and later diagnostics under one durable identity.

The current implementation intentionally executes only top-level steps. Persisted nested `If` / `Loop` cursor frames are already modeled and validated in Core, but their durable execution semantics remain a separate control-flow implementation step.

## AutomationRun

`AutomationRun` is the durable runtime snapshot for one execution.

It currently contains:

```text
RunId
ScenarioId
ScenarioVersionId
RunState
ExecutionCursor
Variables
CreatedAt
UpdatedAt
```

Creation rules:

- a caller may supply `RunId`; an omitted ID is generated,
- `Guid.Empty` is rejected,
- a new run starts in `Queued`,
- its cursor must validate against the exact immutable scenario version,
- a new queued run cannot begin with an already-completed cursor,
- initial variables and cursor data are copied so caller mutation cannot mutate the run snapshot.

Restore rules:

- the same immutable `ScenarioVersion` is required to validate the persisted cursor,
- `UpdatedAt` cannot precede `CreatedAt`,
- a persisted `Completed` run must also have a completed cursor,
- restored variable and cursor state is copied into the domain snapshot.

The variable representation is still string-only at this stage. Structured runtime values remain a separate Sprint 2.5 change.

## Immutable scenario version binding

Before a run becomes durable, its scenario definition is captured as an immutable `ScenarioVersion`.

Each version has:

```text
ScenarioId
VersionId
VersionNumber
SchemaVersion
DefinitionHash
DefinitionJson
CreatedAt
```

`DefinitionJson` is canonical JSON of the normalized scenario definition and `DefinitionHash` is SHA-256 over that canonical representation. This makes the stored definition independently verifiable and prevents incidental JSON property or dictionary ordering from producing different identities.

A running workflow must keep the same `VersionId` from start through completion, suspension, resume, retries, and crash recovery. Editing the reusable scenario creates a later version; it must never rewrite the definition used by an already-started run.

The durable persistence layer stores these snapshots and binds each run to one version. The current SQLite store revalidates the snapshot/hash semantics when loading persisted data.

`ScenarioVersion.Restore` supports process-restart and persistence flows. It restores the original `ScenarioId` and `VersionId`, verifies that persisted JSON is still the canonical normalized representation, and recomputes SHA-256 so corrupted or mismatched stored definitions are rejected.

## Execution cursor

Durable execution uses a versioned `ExecutionCursor` rather than a single numeric step index.

The cursor points to the **next step to execute** and stores the active control-flow stack from outermost to innermost container.

Example:

```json
{
  "version": 1,
  "nextStepId": "read-value",
  "frames": [
    {
      "stepId": "outer-loop",
      "kind": "Loop",
      "nextChildIndex": 0,
      "iteration": 3
    },
    {
      "stepId": "inner-if",
      "kind": "If",
      "nextChildIndex": 0,
      "iteration": null
    }
  ]
}
```

Semantics:

- `nextStepId` is the stable ID of the next scenario step to execute.
- `frames` are ordered from the outermost active control-flow container to the innermost.
- `nextChildIndex` identifies the child path that leads to `nextStepId`.
- a `Loop` frame also stores a zero-based `iteration`.
- an `If` frame must not store an iteration.
- a completed cursor has `nextStepId = null` and no frames.
- cursor JSON is versioned independently from scenario JSON so cursor storage can evolve without silently changing durable resume semantics.

Before resume, the cursor must be validated against the immutable scenario version used by the run. The referenced step and every control-flow frame must still exist and the frame stack must match the ancestry of the next step.

## Execution outcomes

The executor needs a result that distinguishes at least:

```text
Completed
Suspended
Failed
Cancelled
```

`Suspended` is a successful durable transition, not an error.

## Durable run state machine

Persisted run state uses a small status set plus a separate wait reason:

```text
Queued
Running
Waiting
Completed
Failed
Cancelled
```

Only `Waiting` carries a `RunWaitReason`:

```text
Event
Human
Retry
Schedule
```

The legal transitions are:

| From | To |
| --- | --- |
| `Queued` | `Running`, `Cancelled` |
| `Running` | `Waiting`, `Completed`, `Failed`, `Cancelled` |
| `Waiting` | `Running`, `Failed`, `Cancelled` |
| `Completed` | terminal |
| `Failed` | terminal |
| `Cancelled` | terminal |

A run cannot switch directly from one wait reason to another. It must resume to `Running` first, then enter a new `Waiting` state if needed. This keeps the transition history explicit and prevents a persisted wait from being silently reinterpreted.

Wait reason semantics:

- `Event` means progress depends on a matching external event. Event identity/correlation data is persisted separately from the enum.
- `Human` means automatic execution is intentionally blocked until an explicit administrative/user decision is recorded.
- `Retry` means execution may continue automatically after retry policy permits another attempt. The due time/backoff belongs to persisted run metadata, not to the enum.
- `Schedule` means execution is intentionally dormant until a scheduled time. The due timestamp is persisted separately.

`RunState` validates these invariants in Core. A restored `Waiting` state without a reason is invalid, and non-waiting states cannot carry a wait reason.

The future executor outcome `Suspended` is not a persisted run status. It describes the fact that active execution stopped cleanly after the run was durably moved to `Waiting`. This avoids having both `Suspended` and `Waiting` represent the same persisted condition.

## Suspend

A `Suspend` step:

1. resolves variables in its configuration,
2. persists the current run,
3. records what event/correlation should resume it,
4. advances or records the resume position deterministically,
5. returns `Suspended`,
6. allows the current process to exit.

Example:

```json
{
  "type": "Suspend",
  "parameters": {
    "event": "external.response",
    "correlationId": "{{runId}}"
  }
}
```

## Resume

Resume must:

1. load the run,
2. verify that it is resumable,
3. verify event idempotency,
4. acquire a lease/lock,
5. merge allowed event data into the execution context,
6. continue from the saved position,
7. release or renew the lease as appropriate.

Calling resume twice with the same event must not execute the next step twice.

## WaitFor versus Suspend

Use `WaitFor` for browser-local waits:

- selector visibility,
- text visibility,
- URL changes,
- load states.

Use `Suspend` for waits that should survive process termination.

Do not convert normal Playwright synchronization into durable events.

## Browser sessions

Replace the assumption of one singleton browser automation service with explicit sessions.

Suggested abstractions:

```csharp
public interface IBrowserSessionFactory
{
    Task<IBrowserSession> CreateAsync(
        BrowserSessionOptions options,
        CancellationToken cancellationToken = default);
}
```

A session can be:

- ephemeral,
- persistent with a named profile.

Persistent profiles require locking. Two workers must not use the same user-data directory concurrently unless the implementation explicitly supports it.

## Page observers

A page observer is independent of scenario execution.

Example configuration:

```json
{
  "name": "Processing completed",
  "url": "{{resultUrl}}",
  "profile": "portal-prod",
  "condition": {
    "type": "TextEquals",
    "selector": ".status",
    "value": "Completed"
  },
  "pollIntervalSeconds": 30,
  "emit": {
    "type": "processing.completed",
    "correlationId": "{{runId}}"
  }
}
```

Initial generic conditions:

- `SelectorVisible`
- `SelectorHidden`
- `TextEquals`
- `TextContains`
- `TextChanged`
- `UrlMatches`
- `DomChanged`

An observer publishes an `AutomationEvent`. It does not directly invoke application-specific continuation code.

## Event model

Suggested event:

```csharp
public sealed record AutomationEvent
{
    public required Guid EventId { get; init; }
    public required string Type { get; init; }
    public Guid? RunId { get; init; }
    public string? CorrelationId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public Dictionary<string, string> Data { get; init; } = [];
}
```

Core must not know whether the event arrived through:

- HTTP,
- GitHub,
- a queue,
- a local process,
- the web panel,
- a page observer.

## Persistence

The first durable store is implemented through the Application-level `IRunStore` and `IStepAttemptStore` contracts and the Infrastructure-level `SqliteRunStore`.

The current SQLite schema uses:

```text
ScenarioVersions
Runs
StepAttempts
```

`ScenarioVersions` stores immutable canonical scenario snapshots, including `DefinitionHash`. `Runs` stores the stable run identity, scenario-version reference, run status/wait reason, serialized `ExecutionCursor`, string variables, and creation/update timestamps. `StepAttempts` stores the persisted lifecycle of each step execution, including retry-safety classification and failure diagnostics.

Saving a run and its scenario version occurs in one SQLite transaction. Reusing an existing `VersionId` with different immutable scenario data is rejected. Reusing a `RunId` with a different scenario/version identity or creation timestamp is also rejected.

A step attempt must first be persisted as `Started`. The store rejects inserting a new attempt directly as `Completed`, `Failed`, or `Unknown`. Once persisted, the same attempt can transition exactly once from `Started` to a final state, while immutable attempt identity fields remain unchanged. This makes the required "persist before side effect" ordering enforceable by the durable executor.

After a process restart, `MarkStartedAttemptsUnknownAsync` converts still-persisted `Started` attempts into `Unknown` so the recovery policy can decide whether to retry automatically, verify first, or wait for a human.

The database schema uses SQLite `PRAGMA user_version = 2`. Existing version-1 databases are upgraded transactionally by adding the `StepAttempts` table and indexes without replacing existing scenario/run data. The default database path is `data/dabot.db` and can be overridden with `bot.storage.databasePath`.

A persisted run can be loaded by a fresh `SqliteRunStore` instance after process restart. Loading reconstructs and revalidates `ScenarioVersion`, `RunState`, `ExecutionCursor`, variables, `AutomationRun`, and `StepAttempt` data rather than trusting raw database fields.

Planned tables remain:

```text
Scenarios
RunVariables (if variables move out of the run snapshot)
ProcessedEvents
Events
BrowserProfiles
PageObservers
Workers
```

The persistence implementation remains behind Application interfaces.

## Worker model

Workers should be independent of the web UI.

A worker:

1. registers/heartbeats,
2. takes work,
3. acquires a run or profile lease,
4. executes the required browser actions,
5. persists results,
6. publishes events if needed,
7. releases the lease.

The first implementation can run a single local worker. The domain should not prevent multiple workers later.

## Web panel

The panel uses Application services and the same stores as other clients.

Initial pages:

```text
Dashboard
Scenarios
Scenario editor
Runs
Run details
Browser profiles
Configuration
```

Later pages:

```text
Observers
Events
Workers
Schedules
Secrets
Audit
```

### Scenario editor

The editor should offer two synchronized representations:

- visual step editor,
- JSON editor.

JSON remains importable/exportable and runnable from CLI.

### Run details

Run details should expose:

- current status and step,
- step history,
- variables,
- event history,
- screenshots and HTML artifacts,
- errors,
- administrative actions.

## Transport integration

External transports are adapters.

Suggested interfaces:

```csharp
public interface IEventPublisher
{
    Task PublishAsync(
        AutomationEvent automationEvent,
        CancellationToken cancellationToken = default);
}

public interface IEventConsumer
{
    Task HandleAsync(
        AutomationEvent automationEvent,
        CancellationToken cancellationToken = default);
}
```

One possible transport can be used for remote wake-up/notification while SQLite remains the source of truth for run state.


## Step attempts and crash recovery

Every durable step execution is represented by a persisted `StepAttempt`.

A step attempt is created in `Started` state **before** executing the step's potentially side-effecting operation. After the operation returns, the same attempt is finalized as either `Completed` or `Failed`.

If a worker/process disappears while an attempt is still `Started`, recovery marks it `Unknown`. `Unknown` does not mean the step failed; it means DABot cannot prove whether the side effect happened before the process stopped.

The attempt lifecycle is:

```text
Started -> Completed
        -> Failed
        -> Unknown   (worker/process interruption detected)
```

A finalized `Completed` or `Failed` attempt is not reopened.

Each attempt stores at least:

```text
AttemptId
RunId
StepId
StepType
AttemptNumber
RetrySafety
Status
StartedAt
UpdatedAt
FinishedAt
ErrorMessage
```

### Retry safety

DABot separates ordinary retry count from recovery safety. A retry may be allowed by configuration but still be unsafe after an interrupted side effect.

The default classifications are:

| Step type | Default retry safety | Reason |
| --- | --- | --- |
| `ReadText`, `WaitFor`, `Screenshot`, `Delay`, `If`, `Loop` | `SafeToRetry` | No remote mutation is expected. |
| `OpenUrl`, `FillText`, `PasteText` | `Idempotent` | Repeating the same operation is expected to converge to the same browser state. |
| `Click` | `NeedsVerification` | A click may already have submitted or triggered an action. |
| `CallApi` | `NeverRetryAutomatically` | The generic API call may have arbitrary external side effects. |

A scenario can override the default per step with `retrySafety` when the author knows more about the target system.

Recovery maps an interrupted attempt to one of these actions:

```text
SafeToRetry / Idempotent       -> RetryAutomatically
NeedsVerification              -> VerifyBeforeRetry
NeverRetryAutomatically        -> WaitingForHuman
```

`VerifyBeforeRetry` is intentionally not the same as automatic retry. A future verification strategy may inspect browser/API state and only retry when it can prove the side effect did not already happen.

### Examples

If DABot crashes after a `ReadText`, repeating the read is safe.

If it crashes after `FillText`, repeating the same fill is treated as idempotent by default.

If it crashes after `Click`, DABot must not blindly click again. The click may already have submitted a form, sent a message, confirmed an order, or triggered another action.

If it crashes during `CallApi`, the default is `WaitingForHuman` because DABot cannot assume an arbitrary remote endpoint is idempotent. A scenario may opt into a less restrictive policy only when the endpoint semantics are known.

The persistence layer added later must guarantee that the `Started` attempt is committed before the side-effecting operation begins. Without that ordering, crash recovery cannot distinguish "never started" from "possibly executed".

## Failure and recovery rules

- Every run has a stable `RunId`.
- Every event has a stable `EventId`.
- Processed events are recorded.
- Resume is idempotent.
- A worker must use a lease for mutable run execution.
- A persistent browser profile must also have a lease.
- A crash during a step must leave enough data to determine whether retry is safe.
- Non-idempotent actions may require an explicit retry policy.
- Max attempts and max errors are configurable.
- A run can move to `Waiting` with reason `Human` when automatic recovery is unsafe.

## Compatibility

Existing simple scenarios must remain valid.

The following remains a normal synchronous workflow:

```text
OpenUrl
WaitFor
ReadText
Screenshot
Completed
```

The durable model is an extension, not a replacement for the current scenario engine.
