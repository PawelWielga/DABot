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
CurrentStep
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

The executor should no longer be limited to:

```csharp
ExecuteAsync(ScenarioDefinition scenario)
```

The target shape should accept a request containing execution identity and initial state.

Example:

```csharp
public sealed record ScenarioRunRequest
{
    public required ScenarioDefinition Scenario { get; init; }
    public Guid? RunId { get; init; }
    public int? ResumeFromStep { get; init; }
    public Dictionary<string, string> Variables { get; init; } = [];
}
```

The exact API may change, but `RunId` must be externally controllable.

## Execution outcomes

The executor needs a result that distinguishes at least:

```text
Completed
Suspended
Failed
Cancelled
```

`Suspended` is a successful durable transition, not an error.

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

SQLite is the preferred first durable store for the web-enabled runtime.

Suggested tables:

```text
Scenarios
ScenarioVersions
Runs
RunSteps
RunVariables
ProcessedEvents
Events
BrowserProfiles
PageObservers
Workers
```

The persistence implementation stays behind interfaces.

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
- A run can move to `WaitingForHuman` when automatic recovery is unsafe.

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
