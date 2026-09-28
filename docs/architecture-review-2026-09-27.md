# Architecture review and improvement plan

Review date: 2026-09-27

This document captures the architecture review of the current DABot documentation and implementation. It is intentionally focused on changes that should be made before the project grows into a durable multi-worker automation platform.

## Executive summary

The current product direction is strong: DABot is designed as a generic browser automation and durable workflow engine rather than a tool tied to one website or one business process.

The strongest architectural decisions already present are:

- scenario definitions are separate from runtime execution state,
- short browser waits and long-lived suspension are treated as different mechanisms,
- transports such as HTTP, GitHub events, queues, or local events remain infrastructure adapters,
- the web panel is optional and does not own the runtime,
- persistent browser profiles, event idempotency, leases, diagnostics, and Linux-first execution are considered early,
- existing synchronous scenarios are intended to remain compatible as durable execution is added.

The main risk is that the target durable model is currently more advanced than the execution semantics implemented underneath it. Before implementing suspend/resume, observers, workers, or the full web panel, the execution model should be hardened.

## Priority 1: harden the execution model before durable workflows

### Stable scenario schema version

Add a top-level schema version now, while there are still very few scenarios:

```json
{
  "schemaVersion": 1,
  "name": "Submit form",
  "steps": []
}
```

Backward compatibility is already a product requirement, so migrations need an explicit version boundary.

### Stable step identity

Step array indexes are not enough for durable execution.

Each step should have a stable ID:

```json
{
  "id": "submit-order",
  "type": "Click",
  "selector": "#submit"
}
```

The ID should survive edits that do not replace the logical step and should be unique within a scenario version.

### Execution cursor instead of only CurrentStep

A single numeric `CurrentStep` is insufficient once `If`, `Loop`, nested scenarios, or retries exist.

A durable run needs an execution cursor or execution stack capable of representing nested state, for example:

```text
CurrentNodeId: submit-item
Frames:
  loop-products
    iteration: 3
  if-product-available
```

The exact storage format may differ, but resume must be deterministic even when execution stopped inside nested control flow.

### Immutable scenario versions

A run must always execute the exact scenario version it started with.

Recommended relationship:

```text
Scenario
  -> ScenarioVersion
       -> AutomationRun
```

A scenario version should contain at least:

- ScenarioVersionId,
- immutable definition JSON,
- schema version,
- content hash,
- creation timestamp.

Editing a scenario must create a new version rather than mutating the definition used by suspended runs.

## Priority 2: define crash recovery and step attempts

Event idempotency alone does not protect against a process crash during a non-idempotent step.

Example:

```text
1. Click Submit
2. Server accepts the operation
3. DABot crashes before persisting step completion
4. Process restarts
5. The same Click is executed again
```

Introduce persisted `StepAttempt` records.

Suggested lifecycle:

```text
Started -> Completed
        -> Failed
        -> Unknown
```

If the process crashes after a side effect but before completion is persisted, the attempt becomes logically `Unknown`.

Each step type should have retry semantics such as:

- `SafeToRetry`,
- `Idempotent`,
- `NeedsVerification`,
- `NeverRetryAutomatically`.

Unsafe recovery should move a run to `WaitingForHuman` rather than blindly repeating the operation.

This needs to be defined before automatic retry and durable resume are implemented.

## Priority 3: replace the catch-all ScenarioStep model internally

The current `ScenarioStep` model is practical for the MVP but will become difficult to validate as more step types are added.

An external JSON representation can remain compact, but after loading it should be converted to typed internal definitions such as:

- `ClickStepDefinition`,
- `FillTextStepDefinition`,
- `ReadTextStepDefinition`,
- `ApiStepDefinition`,
- `LoopStepDefinition`,
- `SuspendStepDefinition`.

This gives each step a valid set of properties and makes validation easier.

A compilation stage is recommended:

```text
Scenario JSON
   -> deserialize
   -> schema validation
   -> typed scenario model
   -> compile/normalize
   -> ExecutionPlan
```

This also gives DABot a natural place to validate references, assign internal node IDs, and reject unsupported combinations before execution.

## Priority 4: strengthen the variable model

`Dictionary<string, string>` will be restrictive for API responses, loops, conditions, and event payloads.

Variables should support structured values.

A pragmatic first implementation can use JSON values while exposing helpers for common primitives:

- string,
- number,
- boolean,
- null,
- object,
- array.

A future variable model may distinguish a `SecretReference` from normal data.

## Priority 5: formalize run state transitions

Decision adopted: persisted run state uses the compact status set below:

```text
Queued
Running
Waiting
Completed
Failed
Cancelled
```

`Waiting` has a separate reason:

```text
Event
Human
Retry
Schedule
```

Core now defines and tests the legal transition graph. Terminal states cannot transition further, `Waiting` always requires a reason, and non-waiting states cannot carry one. A direct change from one wait reason to another is intentionally rejected; the run must resume to `Running` before entering another wait.

A future executor result may still report `Suspended` as an execution outcome, but the durable persisted state is `Waiting` with the appropriate reason.

## Priority 6: define browser state semantics across Suspend

A durable run persists workflow state, not a live browser DOM.

After:

```text
OpenUrl
Click
Suspend
```

the process may exit. On resume there may be no browser, page, DOM, or current URL.

Persistent profiles preserve session data such as cookies and local storage, but they do not guarantee that an active page continues to exist.

The documentation should explicitly state:

> Durable resume guarantees workflow state, not preservation of the active browser page.

Scenarios that resume after suspension must be able to rebuild the browser state. Future strategies may include:

- reopening a known URL,
- executing a recovery scenario,
- restoring a checkpoint containing navigation data.

## Priority 7: make browser sessions explicit early

The current implementation registers browser automation as a singleton. That is incompatible with multiple independent runs.

Introduce `IBrowserSession` and `IBrowserSessionFactory` before durable run storage.

Recommended session modes:

- ephemeral,
- storage-state based,
- persistent browser context.

Persistent contexts require exclusive profile locking. Storage-state sessions can cover many authenticated workflows without holding a full user-data directory lock.

The scenario executor should own or receive a session scoped to one run. It should not own a global browser service.

## Priority 8: use a richer locator model

CSS selectors should remain supported, but DABot should eventually expose Playwright's more resilient locator strategies.

Suggested logical locator types:

- CSS,
- XPath,
- role,
- label,
- placeholder,
- text,
- test id.

Example:

```json
{
  "type": "Click",
  "locator": {
    "type": "role",
    "role": "button",
    "name": "Send"
  }
}
```

This reduces scenario fragility when frontend markup changes.

## Priority 9: avoid NetworkIdle as the default navigation contract

Using `NetworkIdle` for every navigation can create unnecessary timeouts on SPAs, telemetry-heavy applications, WebSocket pages, and long-polling sites.

Prefer a less opinionated navigation default such as `DOMContentLoaded`, then use explicit `WaitFor` steps when a scenario needs stronger synchronization.

This better matches the declarative model because waiting behavior stays visible in the scenario.

## Priority 10: transactional event handling with inbox/outbox semantics

Persisting processed `EventId` values protects against duplicate delivery, but durable processing also needs protection from partial processing.

For example:

```text
1. Event stored
2. Process crashes
3. Resume work is never queued
```

Use inbox/outbox-style persistence so the state transition and future work are committed atomically where possible.

A typical transaction could perform:

```text
INSERT Event
UPDATE Run
INSERT WorkItem
```

A unique `EventId` prevents duplicate delivery from creating duplicate continuations.

The implementation does not need a distributed message broker initially. SQLite can be the source of truth for the first durable runtime.

## Priority 11: explicit secret model

Secrets should not be normal scenario variables.

Prefer references such as:

```text
{{secret:portal-token}}
```

behind an abstraction such as `ISecretProvider`.

Possible providers can later include:

- environment variables,
- local encrypted storage,
- container secrets,
- platform credential stores.

Logs, run variables, screenshots where practical, and web UI responses must avoid exposing secret values.

## Priority 12: security model before the management panel

The web panel can control authenticated browser profiles and arbitrary automation. That makes it a privileged administrative surface.

Before the panel becomes remotely accessible, document the trust model in a dedicated security document covering at least:

- authentication,
- authorization,
- profile ownership and isolation,
- artifact access,
- secret access,
- audit logging,
- CSRF protection,
- SSRF implications of API and URL steps,
- log redaction,
- network exposure assumptions.

The first web panel can stay local/admin-only, but this needs to be an explicit deployment assumption rather than an accidental one.

## Priority 13: keep the web panel small until the model stabilizes

The panel is a good part of the product direction, but the synchronized visual/JSON workflow editor should not drive the domain model.

Recommended web MVP:

- scenario list,
- structured step editor,
- JSON editor,
- validation,
- test run,
- run list,
- run details,
- artifacts,
- browser profiles.

Advanced drag-and-drop editing can follow after the scenario schema and execution model are stable.

## Priority 14: runtime and dependency cleanup

Before adding the durable layers:

- align Microsoft.Extensions package major versions,
- move to the currently selected supported .NET target for the project,
- update Playwright before implementing new browser-session behavior,
- remove unused template files such as `Class1.cs`,
- keep project package versions centralized if the number of projects grows.

Version upgrades should be isolated from durable-workflow changes so regressions are easier to identify.

## Priority 15: add normal CI and broaden tests

The repository currently has a maintenance workflow but no normal build/test gate.

Add a lightweight CI workflow for pull requests and main:

```text
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

Browser integration tests can run in a separate job or workflow.

Before implementing durable execution, add tests for:

- ScenarioExecutor behavior with a fake browser/session,
- step handler failures,
- variable propagation,
- retries,
- nested control flow,
- execution cursor serialization,
- process-crash recovery semantics,
- suspend/resume,
- duplicate events,
- leases and concurrent workers.

Most workflow semantics should be testable without launching Chromium.

## Recommended execution architecture

A useful target shape is:

```text
ScenarioDefinition
      |
      v
ScenarioCompiler / Validator
      |
      v
ExecutionPlan
      |
      v
AutomationRun
      |
      v
ExecutionCursor
      |
      v
StepAttempt
      |
      v
StepHandler
      |
      +---- BrowserSession
      +---- Http client
      +---- other adapters
```

Events remain a parallel entry path:

```text
AutomationEvent
      |
      v
Event Inbox
      |
      v
RunCoordinator
      |
      v
Resume WorkItem
```

All clients continue to use the same application layer:

```text
CLI --------+
Web --------+---- Application
HTTP API ---+
Worker -----+
```

## Recommended implementation order

The existing roadmap should remain recognizable, but the following sequence reduces expensive redesign later:

1. Finish the practical parts of Sprint 2 that do not depend on durable semantics.
2. Execute Sprint 2.5: execution model hardening.
3. Introduce explicit browser sessions and persistent profiles.
4. Introduce immutable scenario versions and durable run persistence.
5. Add deterministic suspend/resume and crash recovery.
6. Add event inbox/idempotency and resume work items.
7. Add generic page observers.
8. Add the web management MVP.
9. Add multi-worker leases and coordination.
10. Add scheduling, operational hardening, metrics, retention, backup, and audit.

## Design decisions that should remain unchanged

The following existing principles should be preserved:

- DABot remains generic and service-neutral.
- Browser operations remain generic rather than adding vendor-specific step types.
- `WaitFor` remains distinct from durable `Suspend`.
- CLI and workers remain fully usable without the web panel.
- External transports remain infrastructure adapters.
- Scenario definition and mutable run state remain separate.
- Simple synchronous scenarios remain supported.
- Linux remains the primary deployment target.
