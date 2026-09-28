# AGENTS.md

Guidelines for agents working in this repository.

DABot is a general-purpose .NET browser automation engine built around declarative scenarios and Playwright. The current implementation executes synchronous browser scenarios; durable runs, suspend/resume, external-system integrations, events, page observers, a web panel, and MCP integration are roadmap work unless the code and README explicitly state otherwise.

## Product direction

- Keep DABot generic. Do not design Core around one website, vendor, or business process.
- Existing synchronous scenarios must continue to work.
- Add durable execution as an extension of the current scenario model.
- Keep short browser waits separate from long-lived suspension.
- The web panel is optional and must not become a runtime requirement for CLI or workers.
- External transports and persistence mechanisms are infrastructure adapters.
- MCP is an integration boundary, not a separate execution engine. MCP tools must delegate to the same Application/Core capabilities used by CLI, workers, HTTP, and the web panel.
- Agents may author declarative reusable tools/workflows, but must not gain an unrestricted code-evaluation path as part of that feature.
- Read `docs/prd.md`, `docs/product-positioning.md`, `docs/tasks.md`, `docs/durable-workflows.md`, and `docs/mcp-and-dynamic-tools.md` before changes that affect architecture or scope.
- Preserve DABot's differentiation around deterministic reusable browser Tools, durable browser workflows, controlled AI-authored Tools, and one shared execution layer. Prefer adapters over rebuilding generic browser-cloud, stealth/proxy, autonomous-agent, or broad SaaS-integration platforms.

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

These rules describe the planned durable execution architecture. They do not imply that durable execution is already implemented.

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

Currently registered executable step handlers:

- `OpenUrl`
- `Click`
- `FillText`
- `PasteText`
- `WaitFor`
- `Delay`
- `ReadText`
- `Screenshot`

The current domain enum also reserves `CallApi`, `If`, and `Loop`, but they are not executable until handlers are implemented and registered. `Suspend` is planned and is not yet part of the current enum.

String scenario inputs support `{{variableName}}` interpolation. Values may come from prior step outputs or run variables, and `runId` is a built-in execution variable. Missing variables must fail explicitly rather than remain unresolved.

Use `schemas/scenario.schema.json` as the machine-readable scenario contract and `schemas/config.schema.json` as the configuration contract. When scenario/configuration models or handler availability change, update the relevant schemas, examples, tests, README status, and AI instruction files in the same change.

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

## Public repository maintenance

Treat the public repository presentation as part of the product, not as a one-time cleanup task. Whenever implementation or scope changes, update the public-facing material in the same change when it becomes inaccurate or incomplete.

- Keep `README.md` aligned with the code. Features must be marked as available only when they are actually implemented and usable; planned or partial work must remain clearly marked as such.
- Keep the README Quick Start executable against the current repository layout, target framework, configuration model, runner commands, and browser-installation flow.
- When adding or changing user-facing capabilities, add or update runnable examples under `examples/`. Examples must use supported behavior and should avoid brittle third-party dependencies where a self-contained example is practical.
- Keep files under `schemas/` synchronized with the corresponding scenario/configuration models, validation rules, defaults, and executable-handler status.
- Keep `.github/copilot-instructions.md` and path-specific files under `.github/instructions/` synchronized with `AGENTS.md` when cross-cutting agent guidance changes.
- Keep `docs/comparison.md` accurate when DABot's capabilities or boundaries relative to raw Playwright change. Do not use misleading marketing claims.
- Keep architecture, PRD, backlog, and feature-status documentation synchronized with implementation changes. Do not leave completed work marked as planned or planned work presented as released.
- Keep CI configuration and README badges accurate when build, test, target framework, or workflow names change.
- Keep the CI browser smoke test aligned with the documented Quick Start and at least one self-contained executable scenario.
- Keep the MIT license file present unless the project owner explicitly decides to change licensing.
- Keep release-readiness tracking current. When work satisfies or invalidates criteria for the next release, update the relevant release issue/checklist in the same work session.
- Review GitHub repository description and topics when the product scope, primary technology, or discoverability keywords materially change. Update them when access allows; otherwise record the required metadata change explicitly.
- Prefer factual search/discovery terms that describe implemented capabilities, such as browser automation, Playwright, .NET, self-hosting, declarative scenarios, and workflow execution. Do not add keywords solely to attract traffic for features that do not exist yet.
- Before merging a public-facing change, verify that the resulting README, examples, docs, and metadata tell a consistent story about what DABot is, what works today, and what is planned.

## Working rules

- Read the product and architecture docs before making scope changes.
- Keep changes small and coherent.
- Do not revert unrelated user changes.
- Preserve backward compatibility for existing scenario JSON unless a migration is explicitly documented.
- Prefer ASCII in source files unless an existing file requires another convention.
- When changing an interface, update tests and documentation in the same change.
- Documentation and repository discoverability updates required by the change are part of the definition of done, not optional follow-up work.

## Pull request workflow

- Before starting new repository work, inspect all open pull requests for the repository, including Dependabot and other bot-authored PRs. Use a repository-wide open-PR listing rather than an author-scoped search.
- When the user asks to continue work on the repository and any PRs are open, review and resolve those PRs before starting unrelated new work.
- Treat PR review and merge as part of completing the change, not as a separate follow-up step.
- After creating a PR, immediately inspect its diff, changed files, mergeability, and available CI/check results.
- If the PR is correct and mergeable, merge it to `main` in the same work session without waiting for a separate merge instruction.
- If the PR has conflicts or failing checks, fix the problem, update/re-check the PR, and merge it to `main` before moving on to unrelated work.
- For stacked PRs, merge them in a dependency-safe order so that every intended change ultimately lands on `main`.
- After merging, verify the latest `main` CI run. If the merge causes CI to fail, fix that failure before starting unrelated work.
- Do not leave a ready, mergeable PR open at the end of normal repository work unless the user explicitly asks to keep it open, leave it as a draft, or delay the merge.

## When in doubt

Prefer the smallest generic mechanism that solves the requirement without coupling DABot to a particular website or transport.
