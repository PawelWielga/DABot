# Web management panel

`DesktopAutomationBot.Web` is the optional Blazor management client for the same Application layer used by the runner and workers.

It is deliberately not a second automation engine and does not directly own Playwright.

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

Scenario editing, run detail/actions, browser-profile management, configuration editing, event history, and authentication UX remain later Sprint 8/9 work.
