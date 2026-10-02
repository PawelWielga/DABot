# AGENTS.md

Guidelines for agents working in this repository.

DABot is a general-purpose .NET browser automation engine built around declarative scenarios and Playwright. The current implementation supports one-shot scenarios, durable runs, suspend/resume, persistent browser profiles, durable events, page observers, and a Blazor management panel with run controls, scenario JSON management, and browser-profile management. MCP integration and the dynamic Tool registry remain roadmap work unless the code and README explicitly state otherwise.

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
- `src/DesktopAutomationBot.Web` - optional server-hosted Blazor management panel.
- `src/DesktopAutomationBot.Web.Shared` - shared Razor management UI.
- `src/DesktopAutomationBot.Web.Demo` - sample-data Blazor WebAssembly host deployed to GitHub Pages.
- `tests/` - unit and integration tests.

Planned:

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
- Distributed deployments use a central control plane plus execution agents; keep standalone Web/Runner + SQLite fully supported.
- Stable node identity is implemented through `INodeIdentityProvider`; the default file adapter persists a machine-local GUID at `bot.node.identityPath` (`data/node-id` by default). Preserve that identity across restarts, never silently rotate a corrupt identity file, and never share one identity file between distinct nodes.
- Agents should prefer outbound authenticated connectivity to the control plane; do not require inbound public ports on execution nodes.
- Do not use SQLite on a shared/network filesystem as the coordination database for multiple VMs. Target PostgreSQL for the first supported distributed persistence provider.
- Persistent Chromium profile directories are node-local by default; scheduling must respect profile ownership/location.

## Browser rules

- Target runtime is Linux first, especially Ubuntu/Debian compatible systems.
- Default browser mode is headless.
- Headed mode is supported for development, diagnostics, and interactive profile setup.
- Support both ephemeral and persistent browser sessions.
- Persistent browser profiles must use dedicated user-data directories and must not be committed.
- Treat profile cookies, local storage, and equivalent session state as sensitive authentication material even when DABot never stores the user's password.
- Remote interactive browser setup must go through Application-level use cases; the Web project must not directly launch or control Playwright.
- Do not model interactive access as an iframe of the target website. The operator must interact with the real headed browser that owns the selected persistent profile.
- Keep remote-display/VNC/noVNC/WebSocket implementation details in Infrastructure. Do not expose a public reusable VNC/noVNC credential or require inbound public agent ports in the normal topology.
- Interactive profile access must use exclusive locking, short-lived authorization, bounded lifetime/cleanup, and audit. The local lifecycle enforces configurable maximum duration through `bot.interactiveBrowser.maxDurationSeconds`; remote streaming still needs inactivity cleanup and short-lived grants. It must not race a run or observer using the same profile.
- Credentials and MFA values typed by an operator inside an interactive browser must not be captured into scenario variables, configuration, logs, or audit payloads. Operator input must flow directly to the browser/profile rather than through Application models. Interactive-session info and audit records must stay metadata-only: session identity, profile identity, lifecycle timestamps, expiry, and lifecycle reason; do not add target URL, page content, form values, credentials, MFA values, or keystrokes without an explicit security redesign.
- Manual administrative audit records must remain metadata-only. Do not add event payloads, correlation IDs, page content, credentials, MFA values, or operator input to `AdministrativeAuditEvent`; use operational identifiers and outcome metadata only.
- A future human **Take control** flow must explicitly coordinate browser-command ownership before accepting operator input; Playwright and a human must not issue commands concurrently.
- Do not keep `IBrowserAutomation` as a global singleton when session isolation or concurrency is required.
- Add new generic browser operations as focused abstractions/handlers rather than growing a monolith.
- Use Playwright synchronization primitives for short browser-local waits.

## Durable workflow rules

These rules describe the durable execution architecture. Durable execution, suspend/resume, event inbox processing, and page observers are implemented; multi-worker coordination is still planned.

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

The web panel currently provides a dashboard, run list/details and guarded run actions, scenario list/synchronized visual+JSON import/export/validation/editing with one-shot test runs, browser-profile list/create/rename/clear/delete/test operations, local headed interactive-profile session lifecycle, page-observer monitoring plus create/edit management, event-history browsing without raw payload exposure, manual durable-event publishing through Application services, metadata-only structured audit logging for manual resume/cancel/event operations, and restart-required editing of non-secret general and storage runtime settings. Continue toward:

- remote interactive-browser display/input streaming with authorization, inactivity cleanup, and audit,
- safely redacted secret-backed configuration.

Later features may include:

- workers,
- schedules,
- secret management,
- durable/queryable general audit logs.

Configuration editing must go through Application contracts. The current file-backed implementation owns non-secret general and storage `bot` settings, preserves logging/unknown sections, serializes both editors through one file lock, and applies changes after process restart. Storage changes must remain configuration-only unless an explicit migration workflow is designed; never silently move or delete existing database, scenario, artifact, screenshot, or browser-profile data.

Observer editing must go through `IPageObserverManagementService`; changes to observation or event-routing semantics reset the persisted observer snapshot through the management store so stale baselines are not reused. The panel must not duplicate domain logic already available in Application. Shared presentation belongs in `DesktopAutomationBot.Web.Shared`; both the real host and `DesktopAutomationBot.Web.Demo` must render the same shared components. The Pages demo uses dummy Application-service implementations only and must never execute automation, access SQLite, contain credentials, or become a separate UI fork.

## Scenario expectations

Currently supported scenario actions include:

- `OpenUrl`
- `Click`
- `FillText`
- `PasteText`
- `WaitFor`
- `Delay`
- `ReadText`
- `Screenshot`
- `CallApi`
- `If`
- `Loop`
- durable `Suspend`.

Check the scenario schema and current handler/compiled-execution code before changing this list.

Scenario steps may set optional `enabled: false`; omitted/true means enabled. Execution must skip disabled steps without handler invocation, and disabled `If`/`Loop` containers skip their whole subtree. Durable execution must persist cursor advancement for skipped steps without creating a step attempt.

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

1. Continue the Web management MVP without duplicating Application/domain logic.
2. Add run details/actions and richer scenario management.
3. Add distributed worker coordination: node registry, heartbeat, leases/CAS, execution slots, and multi-worker-safe work queues.
4. Add the shared control-plane deployment path: authenticated agents, capability-aware scheduling, node-local profile ownership, PostgreSQL coordination, and Nodes dashboard.
5. Add scheduling and operational hardening.
6. Add the neutral Action/Tool registry and MCP adapters after the execution/runtime boundaries are stable.

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
