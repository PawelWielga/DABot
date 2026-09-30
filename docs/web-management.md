# Web management panel

`DesktopAutomationBot.Web` is the optional Blazor management client for the same Application layer used by the runner and workers.

It is deliberately not a second automation engine and does not directly own Playwright.

The visible management UI lives in `DesktopAutomationBot.Web.Shared`. Both the real server-hosted panel and the static demo render those same Razor components, so the demo must not drift into a separate UI implementation.

## Run locally

From the repository root:

```bash
dotnet run --project src/DesktopAutomationBot.Web
```

The default development profile listens on:

```text
http://localhost:5080
```

The web host uses `bot.storage.databasePath` from its own `appsettings.json`. By default this points to the same repository-local runtime database:

```text
data/dabot.db
```

## Current pages

### Dashboard

The first dashboard is read-only and shows:

- total durable run count,
- running runs,
- waiting/suspended runs,
- failed runs,
- completed runs,
- cancelled runs,
- recent runs,
- recent failures,
- waiting runs,
- a worker-summary placeholder for the future worker registry.

### Runs

The runs page shows up to the latest 100 durable executions from the SQLite runtime database.

The Application contract is `IRunQueryService`; the Blazor components do not issue SQLite queries directly.

### Scenarios

The scenarios page is read-only and lists top-level JSON files from `bot.storage.scenariosDirectory`.

For each file it shows the scenario name, file name, schema version, step count, browser-profile mode, and validation status. Invalid definitions remain visible with validation diagnostics instead of failing the whole page.

The UI uses `IScenarioCatalogQueryService`; filesystem access and JSON loading stay in Infrastructure.

## Architecture

The dependency direction is:

```text
DesktopAutomationBot.Web
        |
        v
DesktopAutomationBot.Application
        ^
        |
DesktopAutomationBot.Infrastructure
```

The web project is a composition root. Runtime/browser behavior remains behind Application interfaces.

The first read model is implemented by `SqliteRunQueryService`. It intentionally uses the existing SQLite runtime schema rather than introducing a second EF Core mapping of durable execution tables.

EF Core remains an option for future panel-owned metadata/write models, but it should not duplicate the durable store merely to render a dashboard.

## Authentication readiness

The host wires ASP.NET Core cookie authentication and authorization, and routing uses `AuthorizeRouteView`.

No login/identity provider is enabled yet, so current routes remain anonymous unless a page is explicitly protected later. The shell is structured so an authentication provider can be added without moving authorization logic into UI components.

## Error handling

- development uses the normal ASP.NET Core diagnostics,
- production routes unhandled host errors through `/error`,
- component rendering is wrapped in an `ErrorBoundary`,
- not-found routes render inside the normal management layout.

## Current scope

The foundation is intentionally read-only.

Scenario editing, run detail/actions, browser-profile management, configuration editing, event history, and authentication UX remain later Sprint 8/9 work. The scenario catalog itself is now available read-only.


## Static GitHub Pages demo

`DesktopAutomationBot.Web.Demo` is a Blazor WebAssembly host for the shared management UI.

Public demo:

```text
https://pawelwielga.github.io/DABot/
```

The demo registers in-memory implementations of `IRunQueryService` and `IScenarioCatalogQueryService`. It contains representative completed, running, waiting, failed, and cancelled runs plus valid and invalid scenario examples.

The demo is intentionally presentation-only:

- it does not execute Playwright,
- it does not read SQLite,
- it does not use browser profiles,
- it does not contain real credentials or runtime data,
- the top bar and banner explicitly identify demo mode.

`.github/workflows/pages-demo.yml` publishes the WASM output after relevant changes reach `main`. The deployment rewrites the base path for the repository Pages URL, creates a `404.html` SPA fallback, and adds `.nojekyll`.

Normal CI also publishes the demo on pull requests so shared-UI changes cannot merge if the static host stops compiling.
