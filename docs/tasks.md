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

- [ ] Add `IHttpAutomationClient` or equivalent neutral abstraction.
- [ ] Implement GET/POST/PUT.
- [ ] Support configurable timeout.
- [ ] Support bearer token without storing secrets in repository files.
- [ ] Map response values into scenario variables.
- [ ] Implement `CallApiStepHandler`.

### Logging and diagnostics

- [ ] Add structured logging.
- [ ] Add file logging.
- [ ] Add per-run log correlation.
- [ ] Capture screenshot on failure.
- [ ] Capture HTML snapshot on failure.
- [ ] Write machine-readable run/error report.
- [ ] Mask configured sensitive values.

### Scenario engine

- [x] Implement variable interpolation.
- [x] Implement `Delay`.
- [ ] Implement `If`.
- [ ] Implement `Loop`.
- [~] Implement step retry. Durable failure/recovery now persists retry timing and can suspend to `Waiting / Retry`; automatic scheduling and legacy synchronous retry remain.
- [~] Use `RetryCount` from the scenario model. Durable execution and recovery enforce it as the number of additional attempts; legacy synchronous execution remains.
- [ ] Validate that every declared step type has a registered handler.
- [ ] Add cancellation support through the full execution stack.
- [ ] Add scenario-level timeout where appropriate.

### Runtime

- [~] Add proper CLI arguments instead of relying only on config. The runner now has explicit `run` and `retry-worker` modes; richer command options remain.
- [ ] Return documented exit codes.
- [ ] Add Linux publish/smoke-test instructions.
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
- [ ] Introduce typed internal step definitions or an equivalent compiled execution model.
- [~] Add a scenario compilation/normalization stage before execution.
- [ ] Add a richer locator abstraction while preserving selector compatibility.

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

- [ ] Replace string-only runtime variables with structured values.
- [ ] Preserve convenient string interpolation for simple scenarios.
- [ ] Introduce `ISecretProvider` or an equivalent abstraction.
- [ ] Keep secret references separate from persisted normal variables.
- [ ] Define redaction rules for logs and diagnostics.

### Browser semantics

- [x] Document that durable resume/recovery restores workflow state, not a live DOM/page.
- [~] Define how a resumed run rebuilds required browser state. Recovery now leaves work resumable without assuming the previous live DOM survives; browser-session reconstruction remains.
- [ ] Avoid `NetworkIdle` as the universal default navigation contract.
- [ ] Keep explicit scenario waits for stronger synchronization.

### Events and durable work

- [ ] Define event inbox semantics.
- [ ] Define resume work-item/outbox semantics.
- [ ] Ensure event acceptance, run transition, and future work scheduling can be committed atomically where possible.
- [ ] Define uniqueness constraints for processed `EventId` values.

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

- [ ] Introduce `IBrowserSession`.
- [ ] Introduce `IBrowserSessionFactory`.
- [ ] Support ephemeral sessions.
- [ ] Support persistent sessions with named profiles.
- [ ] Store persistent profile directories outside version control.
- [ ] Add profile-level locking/lease.
- [ ] Add headed interactive profile setup.
- [ ] Add profile health/test operation.
- [ ] Make session ownership explicit in execution context.
- [ ] Remove browser lifetime assumptions from `ScenarioExecutor`.
- [ ] Add tests for independent concurrent sessions.

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

- [ ] Add `Suspend` step type.
- [ ] Add `SuspendStepHandler`.
- [ ] Persist expected event/correlation data.
- [ ] Save deterministic resume position.
- [ ] Return `Suspended` instead of treating suspension as failure.
- [x] Add application-level `ResumeRun` use case for `Waiting / Retry` runs.
- [ ] Add CLI `resume` command.
- [ ] Add CLI `cancel` command.
- [x] Add `Waiting` / `Human` transition for unsafe automatic recovery.
- [x] Add max-attempt guardrails from `RetryCount` before unattended retry scheduling.
- [x] Persist retry due time with per-step `retryDelayMs` / run `RetryNotBefore`.
- [~] Add unattended retry scheduler and optional exponential backoff strategy. Due-run discovery, bounded sweeps, and continuous single-worker polling are implemented; lease/CAS multi-worker safety and exponential backoff remain.

Acceptance criteria:

- a scenario can execute, suspend, exit, and continue in a new process,
- existing synchronous scenarios still behave normally.

## Sprint 6 - Event model and idempotency

- [ ] Add `AutomationEvent`.
- [ ] Add stable `EventId`.
- [ ] Add `CorrelationId`.
- [ ] Add event payload data.
- [ ] Add `IEventPublisher`.
- [ ] Add `IEventConsumer`.
- [ ] Persist event history.
- [ ] Persist processed event IDs.
- [ ] Resume matching runs from events.
- [ ] Ignore duplicate event delivery safely.
- [ ] Add event retry policy.
- [ ] Add dead-letter/failure handling.
- [ ] Add integration tests for duplicate events.

Transport implementations remain infrastructure details.

Potential transports may include:

- in-process,
- HTTP/webhook,
- GitHub events,
- queue-based transport.

Do not couple Core to any of them.

## Sprint 7 - Generic page observers

Goal: detect page changes independently of scenario execution.

- [ ] Add `PageObserverDefinition`.
- [ ] Add observer persistence.
- [ ] Implement polling worker.
- [ ] Implement `SelectorVisible`.
- [ ] Implement `SelectorHidden`.
- [ ] Implement `TextEquals`.
- [ ] Implement `TextContains`.
- [ ] Implement `TextChanged`.
- [ ] Implement `UrlMatches`.
- [ ] Add optional DOM-fragment change detection.
- [ ] Emit an `AutomationEvent` on match.
- [ ] Support persistent browser profiles.
- [ ] Prevent duplicate events for an unchanged condition.
- [ ] Recover active observers after process restart.
- [ ] Add configurable polling interval and backoff.

Acceptance criteria:

- the observer does not need to keep the original scenario execution alive,
- detected changes use the same generic event model as other event sources.

## Sprint 8 - Web management panel MVP

Technology target: Blazor.

The web panel is optional and uses Application services. It must not directly own Playwright.

### Project foundation

- [ ] Add `DesktopAutomationBot.Web`.
- [ ] Add authentication-ready application shell.
- [ ] Add navigation and error handling.
- [ ] Add SQLite/EF Core integration required by the panel.

### Dashboard

- [ ] Run status counters.
- [ ] Recent runs.
- [ ] Recent failures.
- [ ] Suspended/waiting runs.
- [ ] Worker summary placeholder.

### Scenarios

- [ ] Scenario list.
- [ ] Create/edit/delete scenario.
- [ ] Visual step editor.
- [ ] Reorder steps.
- [ ] Enable/disable steps.
- [ ] Validation UI.
- [ ] JSON editor.
- [ ] Synchronized visual/JSON representations.
- [ ] Import JSON.
- [ ] Export JSON.
- [ ] Test run.

### Runs

- [ ] Run list and filtering.
- [ ] Run detail page.
- [ ] Step history.
- [ ] Variables.
- [ ] Event history.
- [ ] Artifact links/previews.
- [ ] Resume action.
- [ ] Cancel action.
- [ ] Retry action.
- [ ] Clone run.

### Browser profiles

- [ ] Profile list.
- [ ] Create/rename/delete profile metadata.
- [ ] Open interactive session.
- [ ] Clear profile.
- [ ] Test profile.

### Configuration

- [ ] General runtime settings.
- [ ] Storage settings.
- [ ] Safe display of secret-backed configuration.

## Sprint 9 - Observers and events in the web panel

- [ ] Observer list.
- [ ] Observer editor.
- [ ] Condition editor.
- [ ] Poll interval configuration.
- [ ] Event type/correlation configuration.
- [ ] Event history browser.
- [ ] Manual administrative event publishing.
- [ ] Audit manual resume/cancel/event operations.

## Sprint 10 - Worker coordination

- [ ] Add worker identity.
- [ ] Add heartbeat.
- [ ] Add worker registry.
- [ ] Add run lease.
- [ ] Add lease renewal.
- [ ] Add abandoned lease recovery.
- [ ] Add concurrency limits.
- [ ] Add browser profile lease.
- [ ] Add worker status page.
- [ ] Verify safe execution with multiple workers.

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
- Browser profiles are runtime data.
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
9. Add multi-worker coordination and operational features.
10. Add the neutral Action/Tool registry, then MCP server/client adapters and AI-authored declarative tools.

This order keeps the existing runner useful at every stage, avoids making the web panel the owner of core runtime behavior, and prevents persistence/UI code from being built around execution semantics that still need redesign.
