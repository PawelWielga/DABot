# DABot - implementation backlog

This backlog supersedes the older linear task list while preserving the original product requirements.

Status legend:

- [x] implemented
- [ ] planned
- [~] partially implemented

## Sprint 0 - Repository foundation

- [x] Create the .NET solution and Core/Application/Infrastructure/Runner projects.
- [x] Configure project references.
- [x] Add tests projects.
- [x] Add editor and Git repository standards.
- [x] Enable nullable and implicit usings.
- [x] Add basic scenario domain model.
- [x] Add scenario validation.
- [x] Add `AGENTS.md`.
- [x] Add public README with Quick Start and explicit available/planned status.
- [x] Add MIT license.
- [x] Add build/test CI.
- [x] Add runnable scenario examples.
- [x] Add machine-readable JSON Schemas for scenarios and configuration.
- [x] Add contributor guide, documentation index, and GitHub issue/PR templates.
- [x] Add repository-wide and path-specific AI/Copilot instructions.
- [x] Add automated dependency update configuration for NuGet and GitHub Actions.

## Sprint 1 - Browser engine and basic scenarios

- [x] Add Playwright for .NET.
- [x] Add `IBrowserAutomation`.
- [x] Implement Chromium startup.
- [x] Implement `OpenUrl`.
- [x] Implement `Click`.
- [x] Implement `FillText`.
- [x] Implement `PasteText`.
- [x] Implement `ReadText`.
- [x] Implement `WaitFor` for selector/text/URL/load state.
- [x] Implement screenshots.
- [x] Add JSON scenario loading.
- [x] Add `ScenarioExecutor`.
- [x] Add per-run screenshot directory.
- [x] Add a sample scenario.
- [x] Add runner configuration.

## Sprint 2 - Complete the original runner MVP

### API integration

- [x] Add `IHttpAutomationClient` or equivalent neutral abstraction.
- [x] Implement GET/POST/PUT.
- [x] Support configurable timeout.
- [x] Support bearer token without storing secrets in repository files.
- [x] Map response values into scenario variables.
- [x] Implement `CallApiStepHandler`.

### Logging and diagnostics

- [x] Add structured logging. Scenario execution emits structured lifecycle/step events through `Microsoft.Extensions.Logging`.
- [x] Add file logging. Runner writes append-only JSON Lines logs to a configurable path.
- [x] Add per-run log correlation. Scenario logs, file logs, run reports, and diagnostic artifacts share the same `RunId`.
- [x] Capture screenshot on failure.
- [x] Capture HTML snapshot on failure.
- [x] Write machine-readable run/error report.
- [x] Mask configured sensitive values in persisted file logs. Values are sourced from named environment variables and replaced with `***` in messages, exceptions, and structured string properties.

### Scenario engine

- [x] Implement variable interpolation.
- [x] Implement `Delay`.
- [x] Implement `If`. Boolean literals and exact variable references control nested child execution in synchronous and durable runners.
- [x] Implement `Loop`. Non-negative literal or variable-resolved iteration counts execute nested children with persisted durable cursor frames.
- [x] Implement step retry. The synchronous executor retries in-process with `retryDelayMs`; durable execution persists retry timing and suspends to `Waiting / Retry` for scheduled retry.
- [x] Use `RetryCount` from the scenario model as the number of additional attempts in both synchronous and durable execution.
- [x] Validate that every declared step type has a registered handler.
- [x] Add cancellation support through the full execution stack. Cancellation propagates through runner, scenario executors, step handlers, and Playwright operations; one-shot Ctrl+C exits with code 130.
- [x] Add scenario-level timeout. Optional `scenario.timeoutMs` cancels the execution token and records a timeout failure when the whole run exceeds its configured duration.

### Runtime

- [x] Add proper CLI arguments instead of relying only on config. The runner supports `run --scenario <path>`, help, and `retry-worker` while preserving config defaults.
- [x] Return documented exit codes.
- [x] Add Linux publish/smoke-test instructions.
- [x] Add browser installation instructions.
- [x] Add at least one Playwright integration/smoke test.
- [x] Align Microsoft.Extensions package major versions.
- [x] Update Playwright to a current supported version before building new browser features. Verified on Microsoft.Playwright 1.63.0.

## Sprint 2.5 - Execution model hardening

Goal: define deterministic execution and recovery semantics before durable persistence, suspend/resume, and the web editor depend on them.

### Scenario model

- [x] Add top-level `schemaVersion`.
- [x] Add stable step IDs.
- [x] Introduce immutable scenario versions.
- [x] Persist scenario definition hash per version.
- [x] Define migration policy for future scenario schema versions.
- [x] Introduce typed internal step definitions or an equivalent compiled execution model. `ScenarioCompiler` produces immutable typed action/If/Loop nodes from normalized validated scenario JSON.
- [x] Add a scenario compilation/normalization stage before execution. Both synchronous and durable execution enter through `ScenarioCompiler`; handlers still receive materialized compatibility steps behind that boundary.
- [x] Add a richer locator abstraction while preserving selector compatibility. Steps support `Selector`, `Text`, and `TestId` locator strategies while legacy `selector` remains executable and canonical-compatible.

### Execution state

- [x] Replace the assumption that a single numeric `CurrentStep` is sufficient.
- [x] Define `ExecutionCursor` / execution stack semantics for nested `If` and `Loop`.
- [x] Define serialization of the execution cursor.
- [x] Define formal run state transitions.
- [x] Define wait reason semantics for event/human/retry/schedule waits.
- [~] Make run execution always reference one immutable scenario version. Durable execution is version-bound; the legacy synchronous executor remains for backward compatibility.

### Step attempts and recovery

- [x] Add the `StepAttempt` concept.
- [x] Persist attempt start before executing potentially side-effecting work.
- [x] Persist attempt completion/failure.
- [x] Define recovery for attempts left in an unknown state after process failure.
- [x] Classify step retry behavior: safe/idempotent/verification-required/manual.
- [x] Route unsafe automatic recovery to `Waiting` with reason `Human`.
- [x] Document crash behavior for browser actions and API actions.

### Variables and secrets

- [x] Replace string-only runtime variables with structured values. Runtime state now stores JSON-compatible `ScenarioVariableValue` instances and SQLite preserves their JSON types.
- [x] Preserve convenient string interpolation for simple scenarios. Embedded references stringify compactly while exact parameter references preserve structured JSON values.
- [x] Introduce `ISecretProvider` or an equivalent abstraction. The default infrastructure provider resolves named environment-backed secrets.
- [x] Keep secret references separate from persisted normal variables. `CallApi` supports provider-neutral `bearerTokenSecret`; resolved values are not added to run variables.
- [x] Define redaction rules for logs and diagnostics. `docs/secrets-and-variables.md` defines persistence/redaction boundaries and the existing file-log masker remains defense in depth.

### Browser semantics

- [x] Document that durable resume/recovery restores workflow state, not a live DOM/page.
- [~] Define how a resumed run rebuilds required browser state. Recovery now leaves work resumable without assuming the previous live DOM survives; browser-session reconstruction remains.
- [x] Avoid `NetworkIdle` as the universal default navigation contract. `OpenUrl` now waits for the normal `load` event.
- [x] Keep explicit scenario waits for stronger synchronization. `WaitFor` remains the explicit selector/text/URL/load-state synchronization mechanism.

### Events and durable work

- [x] Define event inbox semantics. SQLite persists accepted events independently from run execution and matches active `Waiting / Event` registrations by correlation ID and optional event type.
- [x] Define resume work-item/outbox semantics. Matching accepted events enqueue durable `ResumeWorkItem` rows; the event resume worker processes and finalizes them.
- [~] Ensure event acceptance, run transition, and future work scheduling can be committed atomically where possible. Event acceptance, payload attachment, wait consumption, and resume-work scheduling are one SQLite transaction; the run transitions from Waiting when the durable work item is processed.
- [x] Define uniqueness constraints for processed `EventId` values. `AutomationEvents.EventId` is the primary key and `ResumeWorkItems.EventId` is unique.

### Engineering foundation

- [x] Add normal build/test CI.
- [x] Align Microsoft.Extensions package major versions.
- [ ] Update the project runtime target as a dedicated compatibility change.
- [x] Update Playwright before adding browser-session features. Microsoft.Playwright 1.63.0 is current.
- [x] Remove unused template files.
- [x] Add executor tests using fake browser/session implementations.
- [x] Add tests for nested execution cursor behavior and crash recovery semantics.

Acceptance criteria:

- a nested workflow has a deterministic persisted resume position,
- a run is permanently bound to the scenario version it started with,
- a process crash during a side-effecting step has an explicitly defined recovery outcome,
- duplicate external events cannot schedule duplicate continuations,
- secrets are not represented as ordinary persisted variables,
- the execution semantics are testable without requiring Chromium.

## Sprint 3 - Browser sessions and profiles

Goal: remove the assumption that browser automation is one global singleton and prepare safe persistent sessions.

- [x] Introduce `IBrowserSession`.
- [x] Introduce `IBrowserSessionFactory`.
- [x] Support ephemeral sessions. The Playwright factory creates a fresh browser/session object for every execution.
- [x] Support persistent sessions with named profiles. Optional `scenario.browserProfile` selects a Playwright persistent context.
- [x] Store persistent profile directories outside version control. Profiles default to `data/browser-profiles`, covered by the existing runtime-data ignore rule.
- [x] Add profile-level locking/lease. A cross-process file lease prevents concurrent use of the same named profile.
- [x] Add headed interactive profile setup. `profile setup --profile <name> [--url <url>]` opens the named persistent profile in headed mode until the operator closes it from the CLI.
- [x] Add profile health/test operation. `profile test --profile <name>` opens the persistent profile headlessly and reports whether it can be acquired and launched.
- [x] Make session ownership explicit in execution context. `ScenarioExecutionContext` exposes the owned `IBrowserSession` while retaining the compatibility `BrowserAutomation` view.
- [x] Remove browser lifetime assumptions from `ScenarioExecutor`. Synchronous and durable executors acquire and dispose one session per execution/resume.
- [x] Add tests for independent concurrent sessions.

Acceptance criteria:

- two independent scenarios can run without sharing page/context state,
- a persistent profile preserves login/session data across process restarts,
- conflicting use of the same profile is prevented.

## Sprint 4 - Durable run foundation

Goal: separate reusable scenario definitions from persisted execution state.

- [x] Add `ScenarioRunRequest`.
- [x] Allow externally supplied `RunId`.
- [x] Add `AutomationRun`.
- [x] Add `RunStatus`.
- [x] Track `ExecutionCursor` in the run model.
- [x] Persist string run variables in the initial durable store.
- [x] Track creation and update timestamps.
- [x] Introduce `IRunStore`.
- [x] Add an initial SQLite run store.
- [x] Add SQLite `StepAttempt` persistence with v1 -> v2 schema migration and restart recovery.
- [~] Group all runtime artifacts by stable `RunId`. Durable execution uses the stable run ID for its artifact directory; the legacy synchronous path still uses timestamp-based IDs.
- [x] Define execution outcomes: Completed/Suspended/Failed/Cancelled.
- [x] Ensure a process restart does not invalidate a persisted run.
- [x] Add application-level crash recovery that reconciles run cursor and persisted step attempts.

Acceptance criteria:

- a run can be loaded after process restart,
- executor state is not stored in mutable singleton services,
- the same scenario can have multiple independent runs.

## Sprint 5 - Suspend and resume

Goal: allow a workflow to stop without blocking a process and continue later.

- [x] Add `Suspend` step type.
- [x] Implement `Suspend` as a durable executor control-flow primitive instead of a regular handler.
- [x] Persist expected event/correlation data. Event `Suspend` registers a path-safe durable correlation and optional event-type filter atomically with the waiting run.
- [x] Save deterministic resume position. `Suspend` advances and persists the cursor before entering Waiting.
- [x] Return `Suspended` instead of treating suspension as failure.
- [x] Add application-level `ResumeRun` use case for `Waiting / Retry` runs.
- [x] Add CLI `resume` command. Manual resume continues non-retry waiting runs by stable `RunId`; retry waits remain on the guarded retry path.
- [x] Add CLI `cancel` command. Queued/waiting durable runs can be cancelled idempotently; active `Running` runs are rejected until worker lease/CAS coordination exists.
- [x] Add `Waiting` / `Human` transition for unsafe automatic recovery.
- [x] Add max-attempt guardrails from `RetryCount` before unattended retry scheduling.
- [x] Persist retry due time with per-step `retryDelayMs` / run `RetryNotBefore`.
- [~] Add unattended retry scheduler and optional exponential backoff strategy. Due-run discovery, bounded sweeps, and continuous single-worker polling are implemented; lease/CAS multi-worker safety and exponential backoff remain.

Acceptance criteria:

- a scenario can execute, suspend, exit, and continue in a new process,
- existing synchronous scenarios still behave normally.

## Sprint 6 - Event model and idempotency

- [x] Add `AutomationEvent`.
- [x] Add stable `EventId`.
- [x] Add `CorrelationId`.
- [x] Add event payload data. Payloads are structured JSON values persisted in the inbox and exposed to resumed runs through reserved `event.*` variables.
- [x] Add `IEventPublisher`.
- [ ] Add `IEventConsumer`.
- [x] Persist event history.
- [x] Persist processed event IDs.
- [x] Resume matching runs from events through durable resume work items and `IEventResumeWorker`.
- [x] Ignore duplicate event delivery safely. Re-delivery of the same `EventId` returns the original acceptance result without scheduling a second continuation.
- [x] Add event retry policy. Failed durable resume work items retry with configurable exponential backoff and a maximum-attempt guardrail.
- [x] Add dead-letter/failure handling. Exhausted resume work items transition to durable `DeadLetter` state with the final error and remain queryable through the event inbox store.
- [x] Add integration tests for duplicate events.

Transport implementations remain infrastructure details.

Potential transports may include:

- in-process,
- HTTP/webhook,
- GitHub events,
- queue-based transport.

Do not couple Core to any of them.

## Sprint 7 - Generic page observers

Goal: detect page changes independently of scenario execution.

- [x] Add `PageObserverDefinition`.
- [x] Add observer persistence. Definitions and durable polling snapshots are stored in SQLite schema v6.
- [x] Implement polling worker. `observer-worker` loads due observers and polls them independently of scenario execution.
- [x] Implement `SelectorVisible`.
- [x] Implement `SelectorHidden`.
- [x] Implement `TextEquals`.
- [x] Implement `TextContains`.
- [x] Implement `TextChanged` with a persisted baseline.
- [x] Implement `UrlMatches` using a configured regular expression.
- [x] Add optional DOM-fragment change detection. `DomChanged` hashes locator `outerHTML` with SHA-256, persists the hash baseline, and emits only when the fragment changes.
- [x] Emit an `AutomationEvent` on match through the existing transport-neutral event publisher.
- [x] Support persistent browser profiles through `IBrowserSessionFactory`.
- [x] Prevent duplicate events for an unchanged condition. Boolean conditions are edge-triggered; text changes compare against the persisted observation baseline.
- [x] Recover active observers after process restart from durable observer definitions and snapshots.
- [x] Add configurable polling interval and backoff. Each observer has its own poll interval; failures use persisted exponential backoff capped by worker configuration.

Acceptance criteria:

- the observer does not need to keep the original scenario execution alive,
- detected changes use the same generic event model as other event sources.

## Sprint 8 - Web management panel MVP

Technology target: Blazor.

The web panel is optional and uses Application services. It must not directly own Playwright.

### Project foundation

- [x] Add `DesktopAutomationBot.Web`.
- [x] Add authentication-ready application shell. The Blazor host wires cookie authentication/authorization and uses `AuthorizeRouteView`; no login provider is enabled yet.
- [x] Add navigation and error handling. The shell includes primary navigation, not-found handling, an error boundary, and production exception handling.
- [x] Share management Razor components between the real host and a static demo host.
- [x] Publish a sample-data Blazor WebAssembly demo to GitHub Pages and validate its static output in CI.
- [~] Add SQLite/EF Core integration required by the panel. The first read-only dashboard uses `IRunQueryService` backed by SQLite; EF Core is intentionally deferred until panel-owned write models require it.

### Dashboard

- [x] Run status counters.
- [x] Recent runs.
- [x] Recent failures.
- [x] Suspended/waiting runs.
- [x] Worker summary placeholder.

### Scenarios

- [x] Scenario list. The read-only web page lists JSON scenarios from the configured scenarios directory, including validation status, schema version, step count, and browser profile.
- [x] Create/edit/delete scenario. The web panel can create, edit, and delete top-level scenario JSON files through `IScenarioManagementService` with path-safe filesystem writes.
- [x] Visual step editor. The shared scenario editor exposes scenario metadata and recursive step forms for all current step types, including selector/structured locator editing, retry settings, parameters JSON, nested `If`/`Loop` children, add/remove operations, and type-specific fields.
- [x] Reorder steps. The visual editor exposes move-up/move-down controls for top-level and nested steps; each move updates both the visual collection and the underlying JSON array immediately.
- [x] Enable/disable steps. Scenario steps support optional `enabled` with an omitted/default-true contract; the visual editor exposes the toggle, one-shot and durable execution skip disabled steps/subtrees, and durable skips advance and persist the execution cursor without creating step attempts.
- [x] Validation UI. The JSON editor validates syntax and the existing scenario domain rules before saving and can validate on demand.
- [x] JSON editor. A shared raw JSON editor is available in both the real management host and the GitHub Pages demo.
- [x] Synchronized visual/JSON representations. Visual changes rewrite the raw JSON immediately; valid raw JSON refreshes the visual model when the JSON field loses focus or validation runs. The visual model edits the parsed JSON tree in place so extension fields such as `$schema` and unknown future properties are preserved.
- [x] Import JSON. The shared scenario editor loads a local `.json` file into the editor, applies the existing validation rules, and adopts a safe file name for new scenarios.
- [x] Export JSON. The shared scenario editor downloads the current JSON buffer directly, so export works in both the runtime host and the static GitHub Pages demo.
- [x] Test run. The shared scenario editor can execute the current unsaved JSON through an Application-level one-shot test service, show step results, and cancel an active test; durable `Suspend` scenarios are explicitly rejected from this one-shot path.

### Runs

- [x] Run list and filtering. The run list supports client-side status and run-ID filtering over the latest persisted runs.
- [x] Run detail page. Individual durable runs expose scenario identity/version, state, wait/retry timing, and persisted execution metadata.
- [x] Step history. Run details show persisted step attempts with type, retry safety, status, timing, duration, and errors.
- [x] Variables. Run details show persisted structured variables with JSON value kind and display value.
- [x] Event history. Run details show matched durable events and resume work-item status/timing without exposing raw event payloads.
- [x] Artifact links/previews. Run details list files from the configured screenshot/artifact directories, preview safe image artifacts, and expose diagnostic downloads through path-safe runtime endpoints. Captured HTML is download-only to avoid same-origin execution.
- [x] Resume action. Run details invoke the existing `IDurableRunControlService` for non-retry waiting runs and refresh persisted state after the action.
- [x] Cancel action. Run details can cancel queued/waiting runs with an explicit confirmation step; active `Running` cancellation remains blocked until lease/CAS coordination exists.
- [x] Retry action. Run details expose guarded `Retry now` for `Waiting / Retry` runs; manual retry bypasses `RetryNotBefore` while preserving retry limits and recovery checks.
- [x] Clone run. Run details can create a fresh durable execution from the same immutable scenario version. The clone starts from the beginning with a new run ID and reconstructed source inputs; prior run-derived outputs and `runId` are not carried forward.

### Browser profiles

- [x] Profile list. The management panel lists persistent profile directories through `IBrowserProfileCatalog`; the filesystem adapter ignores internal `.locks` state and the static demo uses representative sample profiles.
- [x] Create/rename/delete profile metadata. The web panel uses `IBrowserProfileManagementService`; filesystem mutations validate path-safe names, share the normal profile lease, require delete confirmation in the UI, and reject symlink/reparse-point profile directories.
- [x] Open interactive session from the profile page using a headed browser on the runtime node. `IInteractiveBrowserSessionService` owns the session beyond the web request, exposes active-session state, and supports explicit close.
- [ ] Stream the real runtime browser display and keyboard/mouse input to the management panel; do not rely on embedding the target site in an iframe.
- [x] Add an exclusive profile/session lease so interactive login cannot overlap a run or observer using the same persistent profile. Interactive setup opens the profile through the normal `IBrowserSessionFactory`, so it holds the same cross-process `.locks/<profile>.lock` lease as runs, observers, tests, and profile mutations.
- [~] Add administrator-only authorization, short-lived session grants, inactivity/max-duration cleanup, and audit entries for interactive sessions. Configurable max-duration cleanup is implemented with `bot.interactiveBrowser.maxDurationSeconds` (default 1800), and start/end lifecycle events are emitted through a dedicated non-sensitive audit sink. Administrator authorization, short-lived remote-display grants, inactivity cleanup, and a durable/queryable audit store remain.
- [x] Ensure usernames/passwords/MFA values typed in the interactive browser are not captured as DABot configuration, scenario variables, or logs. Interactive browser input goes directly to the headed browser/profile; Application session and audit contracts retain metadata only, and contract tests prevent URL/page/input fields from being added accidentally.
- [ ] Add an optional later **Take control** flow for diagnostics, gated by explicit browser-command ownership coordination with the active run.
- [x] Clear profile. The web panel clears browser-managed profile contents while preserving the named profile directory; the operation requires confirmation, shares the exclusive profile lease, and rejects symlink/reparse-point profiles.
- [x] Test profile. The profiles page invokes the existing Application-level headless profile health check, which acquires the normal exclusive profile lease before launching Chromium.

### Configuration

- [x] General runtime settings. The shared Configuration page edits browser, retry/event/observer worker, interactive-browser lifetime, and default scenario settings through `IGeneralRuntimeSettingsService`; the runtime host persists only those owned sections to `appsettings.json` and clearly requires a process restart before changes take effect.
- [x] Storage settings. The Configuration page edits scenario, screenshot, artifact, persistent-profile, and SQLite paths through `IStorageRuntimeSettingsService`; path validation rejects empty/root/colliding directory targets, saves require an explicit no-migration acknowledgement, changes apply after restart, and existing files are never moved or deleted.
- [ ] Safe display of secret-backed configuration.

## Sprint 9 - Observers and events in the web panel

- [x] Observer list. The shared web panel lists persisted observer definitions and polling snapshots through `IPageObserverQueryService`, including enabled state, condition, profile, event correlation, polling times, and current error state; the static demo uses representative observer data.
- [x] Observer editor. The shared panel can create and edit persisted observer definitions through `IPageObserverManagementService`; the GitHub Pages demo uses the same Application use case with an in-memory management store.
- [x] Condition editor. The observer form supports every current `PageObserverConditionKind`, structured selector/text/test-id locators, exact matching where applicable, expected values, and URL regular expressions while leaving validation authoritative in Core.
- [x] Poll interval configuration. Observer polling intervals are editable in milliseconds and validated by the existing observer domain rules.
- [x] Event type/correlation configuration. Observer event type and correlation ID are editable; changes that alter observation or event-routing semantics reset the persisted polling snapshot so stale edge/baseline state cannot suppress or fabricate events.
- [ ] Event history browser.
- [ ] Manual administrative event publishing.
- [ ] Audit manual resume/cancel/event operations.

## Sprint 10 - Distributed workers and shared control plane

Goal: allow several DABot agents running on different VMs or physical machines to share one central dashboard, durable queue, and coordination layer while preserving standalone SQLite deployment.

Architecture source: [Distributed DABot deployment](distributed-deployment.md).

### Node identity and registry

- [ ] Introduce stable `NodeId` / worker identity.
- [ ] Add durable node registry.
- [ ] Add node metadata: display name, OS, DABot version, browser versions, tags/capabilities, configured execution slots.
- [ ] Add heartbeat with persisted `LastSeenAt`.
- [ ] Define node lifecycle states: Online, Draining, Offline, Disabled, Unhealthy.
- [ ] Add heartbeat-expiry/liveness evaluation.
- [ ] Add drain, enable, and disable application use cases.
- [ ] Ensure node administrative actions are auditable.

### Run claiming and leases

- [ ] Add atomic run lease / compare-and-swap claiming.
- [ ] Persist lease owner node, lease token/version, acquisition time, and expiry.
- [ ] Add lease renewal for active execution.
- [ ] Reject progress/finalization writes from a node that no longer owns the lease.
- [ ] Add abandoned/expired lease recovery through the existing step-attempt recovery rules.
- [ ] Make retry-worker claiming safe with multiple workers.
- [ ] Make event-resume work claiming safe with multiple workers.
- [ ] Make observer work claiming safe with multiple workers.
- [ ] Add concurrency tests proving that two nodes cannot execute the same run concurrently.

### Capacity and scheduling

- [ ] Add per-node execution-slot/concurrency limits.
- [ ] Report active-run count and capacity in node heartbeats/status.
- [ ] Add node capabilities/tags.
- [ ] Add optional run/scenario capability requirements.
- [ ] Add capability-aware node selection.
- [ ] Add deterministic scheduler behavior when several nodes are eligible.
- [ ] Ensure Draining/Disabled/Unhealthy nodes do not receive new work.

### Browser profile ownership

- [ ] Model persistent browser-profile ownership/location by node.
- [ ] Route runs requiring a node-local profile to the owning node.
- [ ] Keep the existing local profile lease and combine it with distributed run ownership.
- [ ] Prevent profile use on a second node unless an explicit migration/restore operation has occurred.
- [ ] Document that active Chromium user-data directories must not be shared through generic network storage.

### Control plane and agent communication

- [ ] Define Application-level contracts for node registration, heartbeat, work claim/assignment, lease renewal, progress, completion, and artifact reporting.
- [ ] Define Application-level lifecycle contracts for remote interactive browser sessions without coupling Core to VNC/noVNC or another display protocol.
- [ ] Route an interactive profile session to the node that owns the selected persistent profile.
- [ ] Add an authenticated browser-display/input tunnel through the control plane/agent path using short-lived per-session authorization.
- [ ] Keep the normal agent topology outbound-only; do not require a public VNC/noVNC port on execution nodes.
- [ ] Add deterministic timeout/disconnect cleanup for headed browsers, display bridges, and profile locks.
- [ ] Add an authenticated agent-server HTTP API as the first distributed transport.
- [ ] Prefer outbound agent connections so execution VMs do not require inbound public ports.
- [ ] Keep transport-specific models out of Core.
- [ ] Add per-node authentication/authorization and credential rotation strategy.
- [ ] Add compatibility/version negotiation between agent and server where protocol changes require it.
- [ ] Evaluate SignalR/WebSocket push only after the polling/API contract is stable.

### Distributed persistence

- [ ] Keep SQLite as the supported standalone store.
- [ ] Add a shared relational persistence provider for distributed coordination, targeting PostgreSQL first.
- [ ] Implement transactional/CAS run claiming in the distributed provider.
- [ ] Persist cluster-wide nodes, leases, runs, events, schedules, and audit metadata centrally.
- [ ] Add schema migration strategy for the distributed database.
- [ ] Add integration tests against the distributed database provider.
- [ ] Explicitly reject/document shared-network-file SQLite as an unsupported multi-VM coordination topology.

### Artifacts and diagnostics

- [ ] Add node identity to run execution/diagnostic history.
- [ ] Define artifact upload or central-reference contract.
- [ ] Preserve stable `RunId` and step identity across artifact transfer.
- [ ] Support temporary node-local artifact storage with cleanup after successful transfer/retention expiry.
- [ ] Show node/lease/recovery information in run details.

### Web dashboard

- [ ] Replace the current worker-summary placeholder with real cluster data.
- [ ] Add Nodes / Workers list.
- [ ] Show node status, heartbeat age, version, OS, capabilities, capacity, active runs, and recent errors.
- [ ] Add node detail page with execution history.
- [ ] Add Drain / Enable / Disable actions with confirmation and audit.
- [ ] Show assigned/current node on run list and run detail.
- [ ] Aggregate run counters across all connected nodes.

### Deployment and operations

- [ ] Define standalone deployment: Web + workers + Playwright + SQLite.
- [ ] Define distributed deployment: Control Plane + PostgreSQL + one or more Agents.
- [ ] Add Docker/systemd examples for the control plane and agent roles.
- [ ] Document TLS and node credential provisioning.
- [ ] Add health endpoints for control plane and agents.
- [ ] Add multi-node upgrade/version-skew guidance.
- [ ] Add failure tests for node crash, control-plane restart, expired lease, network interruption, and duplicate claim attempts.

Acceptance criteria:

- two agents on separate VMs can register with one control plane and appear in one dashboard,
- a centrally queued run is executed by exactly one eligible agent,
- concurrent claims cannot produce duplicate run execution,
- an expired agent lease invokes the existing safe recovery semantics instead of blindly replaying side effects,
- node-local persistent profiles remain single-owner/single-user,
- the dashboard shows cluster-wide runs and node health,
- standalone SQLite mode remains fully usable,
- distributed mode uses a supported shared database rather than SQLite on a network share.

## Sprint 11 - Scheduling and operations

- [ ] Add scenario schedules.
- [ ] Add observer schedules if needed.
- [ ] Add secret management abstraction.
- [ ] Add retention policies for runtime data and artifacts.
- [ ] Add backup/restore documentation for SQLite and profiles.
- [ ] Add health endpoints.
- [ ] Add metrics.
- [ ] Add audit log.
- [ ] Add Docker deployment option.
- [ ] Add service/systemd deployment documentation.

## Sprint 12 - MCP and dynamic tools

Goal: expose DABot as a reusable execution layer for agents without creating a second automation engine.

### Action and tool model

- [ ] Introduce a neutral Action registry for trusted low-level primitives.
- [ ] Define a versioned declarative Tool/Workflow definition with named inputs and outputs.
- [ ] Add schema validation for tool inputs, outputs, and step parameters.
- [ ] Add tool lifecycle states such as Draft/Enabled/Disabled.
- [ ] Add tool version history and immutable run-time version references.
- [ ] Add permission metadata per Action and Tool.
- [ ] Add policy modes: manual only, AI drafts, AI safe auto-enable, fully autonomous.
- [ ] Require explicit approval for configured high-risk capabilities such as unrestricted shell/filesystem/credentials.
- [ ] Persist audit metadata for tool creation, edits, tests, activation, and disable operations.

### MCP server

- [ ] Add `DesktopAutomationBot.Mcp` without introducing MCP types into Core.
- [ ] Expose active DABot Tools/Workflows as MCP tools.
- [ ] Map MCP tool calls to Application use cases.
- [ ] Expose selected built-in management operations such as tool list/create/update/test/enable/disable.
- [ ] Refresh or notify clients when the active tool set changes.
- [ ] Preserve normal run tracking, diagnostics, cancellation, permissions, and audit behavior for MCP calls.

### MCP client

- [ ] Add an MCP client abstraction in Application.
- [ ] Add infrastructure implementation for connecting to configured external MCP servers.
- [ ] Import/discover external tool metadata without copying protocol types into Core.
- [ ] Add allowlists and per-server permissions.
- [ ] Add timeout, cancellation, retry, and diagnostics.
- [ ] Prevent external MCP tools from silently escalating local DABot permissions.

### Web panel

- [ ] Add Tools page.
- [ ] Add visual/JSON/YAML tool editor.
- [ ] Add tool test action and test history.
- [ ] Add enable/disable/version history.
- [ ] Show source/author such as manual, imported, or AI-generated.
- [ ] Show required permissions and approval state.
- [ ] Add MCP connections/status page.

Acceptance criteria:

- an agent can create a declarative tool from allowed Actions without writing arbitrary executable code,
- a created tool can be validated and tested before activation,
- active tools can be exposed through MCP and invoked through the same Application layer as CLI/web/HTTP,
- DABot can call an allowlisted external MCP server through a controlled adapter,
- disabling or changing a tool does not mutate historical run definitions.

## Cross-cutting requirements

These apply to every sprint.

### Compatibility

- Existing scenario JSON should remain compatible unless a migration is explicitly documented.
- CLI must continue to work without the web panel.
- Web UI must not duplicate application/domain logic.

### Cross-platform

- Linux remains the deployment target.
- Windows remains supported for development/testing.
- Avoid Windows-only dependencies.
- Use portable filesystem paths.

### Security

- No secrets in source control.
- No credentials in logs.
- Browser profiles are runtime data and their cookies/storage must be treated as sensitive authentication material.
- Remote interactive browser sessions must be authenticated, short-lived, auditable, and must not expose reusable remote-desktop credentials or public VNC/noVNC ports.
- External integrations receive minimum required permissions.
- Administrative web actions are auditable.

### Testing

For new behavior add:

- domain tests,
- application orchestration tests,
- persistence tests where relevant,
- Playwright integration tests where relevant,
- suspend/resume tests,
- idempotency tests,
- concurrency tests before enabling parallel workers.

## Near-term implementation order

The recommended next sequence is:

1. Finish the practical missing runner capabilities from Sprint 2.
2. Complete Sprint 2.5 and freeze the durable execution semantics.
3. Introduce browser session factory and persistent profiles.
4. Add immutable scenario versions, durable run model, execution cursor persistence, and SQLite storage.
5. Add `Suspend` / `Resume` with step-attempt recovery semantics.
6. Add event inbox/idempotency and durable resume work items.
7. Add page observers.
8. Add the web panel MVP.
9. Add the distributed worker/control-plane foundation: node registry, heartbeat, leases/CAS, multi-worker-safe queues, capacity limits, and Nodes dashboard.
10. Add the distributed deployment path: authenticated agents, capability-aware scheduling, node-local profile ownership, PostgreSQL coordination, and artifact transfer.
11. Add scheduling and remaining operational hardening.
12. Add the neutral Action/Tool registry, then MCP server/client adapters and AI-authored declarative tools.

This order keeps the existing runner useful at every stage, avoids making the web panel the owner of core runtime behavior, and prevents persistence/UI code from being built around execution semantics that still need redesign.
