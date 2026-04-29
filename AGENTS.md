# AGENTS.md

Guidelines for agents working in this repository. This project is the MVP of
Desktop Automation Bot (DABot): a .NET 8 / C# Linux-first CLI worker that
automates browser workflows with Playwright and talks to a local REST API.

## Project goal

- Build a stable, extensible bot runner for Linux.
- Keep Windows as a development convenience only; do not depend on Windows-only
  APIs for MVP.
- Prefer headless Chromium on Linux as the default runtime.
- Keep the architecture modular so future UI, queueing, OCR, and AI features can
  be added without rewriting the core.

## Repository shape

- `src/DesktopAutomationBot.Core` holds domain models and validation rules.
- `src/DesktopAutomationBot.Application` holds orchestration, use cases, and
  abstractions.
- `src/DesktopAutomationBot.Infrastructure` holds Playwright, HTTP, storage,
  logging, and other external integrations.
- `src/DesktopAutomationBot.Runner` is the console entry point and composition
  root.
- `tests/` holds unit and integration tests.

## Non-negotiable constraints

- Target runtime is Linux first, especially Ubuntu/Debian compatible systems.
- Default browser mode is headless.
- Do not introduce hard dependencies on WPF, Windows registry, DPAPI as the
  only secret mechanism, or other Windows-only infrastructure.
- Build file paths with `Path.Combine` or `Path.Join`. Do not concatenate
  separators manually.
- Keep secrets out of source control and out of logs.
- Keep the codebase cross-platform where possible.

## Architecture rules

- Keep domain models in `Core`.
- Keep use-case orchestration in `Application`.
- Keep Playwright, HTTP, filesystem, and log sink implementations in
  `Infrastructure`.
- Keep CLI parsing and startup wiring in `Runner`.
- Add new browser steps as separate handlers instead of growing a monolith.
- Do not leak Playwright types into the domain model unless absolutely required.
- Prefer interfaces in `Application` and implementations in `Infrastructure`.

## Working rules

- Read `docs/prd.md` and `docs/tasks.md` before making changes that affect scope.
- Follow the sprint order in `docs/tasks.md` unless the user explicitly asks for a
  different priority.
- Keep changes small and coherent.
- Do not revert user changes or unrelated edits.
- Use ASCII by default in code and docs unless the file already uses a different
  convention.
- When editing files, prefer `apply_patch`.

## Current implementation priorities

1. Repo foundation: solution, project structure, standards, and tests.
2. Playwright setup and basic browser actions.
3. API client, logging, screenshots, HTML artifacts, and retry behavior.
4. Scenario executor, variable replacement, `If`, `Loop`, and other control
   flow.
5. Runner polish, sample scenarios, documentation, and Linux smoke testing.

## Scenario and runtime expectations

- Scenario definitions are JSON-based.
- Supported MVP step types include `OpenUrl`, `Click`, `FillText`, `PasteText`,
  `WaitFor`, `ReadText`, `Screenshot`, `CallApi`, `Delay`, `If`, and `Loop`
  where implemented.
- Every run should produce useful logs and, on failure, diagnostic artifacts such
  as screenshot and HTML capture.
- Keep runtime output in predictable folders such as `logs/`,
  `screenshots/`, and `artifacts/`.

## Testing expectations

- Add or update tests when behavior changes.
- Prefer unit tests for validation and orchestration logic.
- Add integration coverage for Playwright and API behavior when practical.
- Keep tests Linux-friendly and headless-friendly.

## Documentation expectations

- Update docs when setup, runtime behavior, or required commands change.
- Keep README and task documentation aligned with the actual implementation.
- If you add a new dependency or runtime requirement, document the Linux impact.

## When in doubt

- Optimize for Linux compatibility, maintainability, and testability.
- Choose the smallest change that advances the current sprint safely.
- If a change has platform or architecture implications, surface the tradeoff in
  the response before making it large.
