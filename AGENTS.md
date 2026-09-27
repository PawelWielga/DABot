# AGENTS.md

Guidelines for agents working in this repository.

DABot is a general-purpose .NET browser automation and durable workflow engine. It executes declarative scenarios, controls Chromium through Playwright, integrates with external systems, and is being extended with persisted runs, suspend/resume, events, page observers, and an optional web management panel.

## Product direction

- Keep DABot generic. Do not design Core around one website, vendor, or business process.
- Existing synchronous scenarios must continue to work.
- Add durable execution as an extension of the current scenario model.
- Keep short browser waits separate from long-lived suspension.
- The web panel is optional and must not become a runtime requirement for CLI or workers.
- External transports and persistence mechanisms are infrastructure adapters.
- MCP is an integration boundary, not a separate execution engine. MCP tools must delegate to the same Application/Core capabilities used by CLI, workers, HTTP, and the web panel.
- Agents may author declarative reusable tools/workflows, but must not gain an unrestricted code-evaluation path as part of that feature.
- Read `docs/prd.md`, `docs/tasks.md`, `docs/durable-workflows.md`, and `docs/mcp-and-dynamic-tools.md` before changes that affect architecture or scope.

## Repository shape

Current projects:

- `src/DesktopAutomationBot.Core` - domain models and validation.
- `src/DesktopAutomationBot.Application` - orchestration, use cases, and abstractions.
- `src/DesktopAutomationBot.Infrastructure` - Playwright and external integrations.
- `src/DesktopAutomationBot.Runner` - CLI/worker composition root.
- `tests/` - unit and integration tests.

Planned:

- `src/DesktopAutomationBot.Web` - optional Blazor management panel.
- `src/DesktopAutomationBot.Mcp` - optional MCP server/client adapter over Application services.

## Architecture rules

- Domain models belong in Core.
- Use-case orchestration belongs in Application.
- Playwright, HTTP, persistence, event transports, filesystem, and logging implementations belong in Infrastructure.
- CLI parsing and worker startup belong in Runner.
- The web project must use Application services. It must not directly control Playwright.
- Do not leak Playwright types into Core.
- Do not leak MCP SDK/protocol types into Core or scenario domain models.
- Treat low-level Actions as trusted implementation primitives and reusable Tools/Workflows as declarative compositions of those primitives.
- Dynamic tool creation must use validation, permissions, versioning, testing, and audit metadata before activation.
- Prefer interfaces in Application and implementations in Infrastructure.
- Keep scenario definition separate from runtime state.
- A scenario describes what to execute. A run describes the state of one execution.
- Do not store mutable execution state by modifying the scenario definition.
- `RunId` must be stable and externally passable for durable execution.
- Resume processing must be idempotent.
- Multiple workers must use leases/locks for mutable run state and persistent browser profiles.

## Browser rules

- Target runtime is Linux first, especially Ubuntu/Debian compatible systems.
- Default browser mode is headless.
- Headed mode is supported for development, diagnostics, and interactive profile setup.
- Support both ephemeral and persistent browser sessions.
- Persistent browser profiles must use dedicated user-data directories and must not be committed.
- Do not keep `IBrowserAutomation` as a global singleton when session isolation or concurrency is required.
- Add new generic browser operations as focused abstractions/handlers rather than growing a monolith.
- Use Playwright synchronization primitives for short browser-local waits.

## Durable workflow rules

- `WaitFor` is for short waits inside an active browser execution.
- `Suspend` is for waits that should survive process termination.
- `Suspend` is not an error condition.
- Persist run state before returning a suspended result.
- Resume from a deterministic saved position.
- Every external event must have an idempotency identity.
- Duplicate event delivery must not execute the same continuation twice.
- Page observers emit generic automation events rather than calling scenario-specific continuation code directly.
- Transport-specific details must not enter Core.

## Storage rules

- Keep storage behind interfaces such as `IRunStore` and `IScenarioStore`.
- SQLite is the preferred first durable store for the web-enabled runtime.
- JSON/local files remain acceptable for simple configuration and import/export.
- Runtime artifacts belong in predictable per-run paths.
- Keep secrets out of source control and out of logs.
- Build file paths with `Path.Combine` or `Path.Join`.

## Web panel rules

The planned web panel should initially cover:

- dashboard,
- scenarios,
- visual + JSON scenario editor,
- runs,
- run details,
- browser profiles,
- configuration.

Later features may include:

- page observers,
- event history,
- workers,
- schedules,
- secret management,
- audit logs.

The panel must not duplicate domain logic already available in Application.

## Scenario expectations

Supported or planned generic step types include:

- `OpenUrl`
- `Click`
- `FillText`
- `PasteText`
- `WaitFor`
- `ReadText`
- `Screenshot`
- `CallApi`
- `Delay`
- `If`
- `Loop`
- `Suspend`

Do not add service-specific step types when the same behavior can be expressed through generic browser steps and events.

## Runtime and diagnostics

Every run should produce useful diagnostics:

- step outcomes,
- timestamps/durations,
- logs,
- screenshots on failure,
- HTML snapshots where useful,
- event history for durable runs.

A restart between `Suspend` and `Resume` must be supported.

## Current implementation priorities

1. Finish the original stable runner requirements: API, logging, retry, variables, control flow, diagnostics, Linux runtime.
2. Add explicit browser session management and persistent profiles.
3. Introduce `ScenarioRunRequest`, `AutomationRun`, statuses, and durable run storage.
4. Add `Suspend` / `Resume` with idempotency.
5. Add neutral event abstractions and event history.
6. Add generic page observers.
7. Add the optional Blazor management panel.
8. Add worker coordination, leases, schedules, and operational hardening.

## Testing expectations

- Add or update tests whenever behavior changes.
- Prefer unit tests for domain validation and orchestration.
- Add integration tests for persistence and Playwright behavior.
- Add tests for suspend/resume and duplicate event delivery.
- Add concurrency tests before enabling multiple workers.
- Keep tests Linux-friendly and headless-friendly.

## Working rules

- Read the product and architecture docs before making scope changes.
- Keep changes small and coherent.
- Do not revert unrelated user changes.
- Preserve backward compatibility for existing scenario JSON unless a migration is explicitly documented.
- Prefer ASCII in source files unless an existing file requires another convention.
- When changing an interface, update tests and documentation in the same change.

## When in doubt

Prefer the smallest generic mechanism that solves the requirement without coupling DABot to a particular website or transport.
