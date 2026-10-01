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

The runs page shows up to the latest 100 durable executions from the SQLite runtime database and supports filtering by status and run ID.

Each run links to a detail page showing:

- run/scenario/version identity,
- current status and wait reason,
- created/updated/retry timing,
- persisted step-attempt history,
- attempt retry-safety classification and errors,
- persisted structured variables and their JSON value kinds,
- matched durable events and resume-work status, retry timing, and errors,
- screenshots and diagnostic artifacts grouped by run, including inline image previews and downloads.

The Application contract is `IRunQueryService`; the Blazor components do not issue SQLite queries directly. `SqliteRunQueryService` reads the existing durable `Runs`, `ScenarioVersions`, `StepAttempts`, `AutomationEvents`, and `ResumeWorkItems` tables without introducing a second persistence model.

Raw event payloads are intentionally excluded from the web read model. The current panel does not yet have production authentication or a dedicated secret-redaction policy for arbitrary event payloads, so event history exposes operational metadata only.

Run artifact access is provided by `IRunArtifactService` with a filesystem implementation in Infrastructure. Only direct regular files below the configured `screenshots/<runId>` and `artifacts/<runId>` directories can be opened; path segments, traversal, symlinks, and reparse points are rejected. Common raster images can be previewed inline. Captured HTML is always returned as a download rather than rendered under the management-panel origin, preventing captured page scripts from executing as panel content.

Run details also expose guarded manual controls through the existing `IDurableRunControlService`:

- Resume is available for waiting runs except retry waits.
- Retry now is available only for `Waiting / Retry` runs. It bypasses the scheduled `RetryNotBefore` time while preserving persisted retry limits and recovery checks.
- Cancel is available for queued and waiting runs and requires an explicit confirmation step in the UI.
- Active running executions are not cancelled from the panel because safe cross-process cancellation requires worker lease/CAS coordination.
- The GitHub Pages demo renders the controls as unavailable and never executes runtime actions.

These are mutating administrative actions. The current host still has no configured login provider, so the management panel should remain on a trusted/local network until authentication and authorization are enabled.

### Scenarios

The scenarios page lists top-level JSON files from `bot.storage.scenariosDirectory`.

For each file it shows the scenario name, file name, schema version, step count, browser-profile mode, and validation status. Invalid definitions remain visible with validation diagnostics instead of failing the whole page.

Each scenario can be opened in the shared raw JSON editor. The editor supports:

- create,
- edit,
- import a local `.json` definition into the editor with a 1 MiB size limit,
- export the current JSON buffer as a local `.json` download,
- explicit validation,
- save only when JSON syntax and scenario-domain validation pass,
- delete.

Import does not bypass the normal save rules. Imported content is validated immediately, and saving still uses `IScenarioManagementService` with the existing path-safe file-name checks. Export is generated from the current editor buffer and does not require a runtime endpoint, so the same behavior is available in the static GitHub Pages demo.

The UI uses `IScenarioCatalogQueryService` for listing and `IScenarioManagementService` for document operations. The filesystem implementation accepts only simple `.json` file names, rejects directory traversal, writes through a temporary file, and keeps all file IO in Infrastructure.

### Browser profiles

The profiles page lists persistent browser profile directories through the Application-level `IBrowserProfileCatalog` contract. The Infrastructure adapter reads only names beneath `bot.storage.browserProfilesDirectory`, ignores internal dot-prefixed directories such as `.locks`, and does not launch Chromium or acquire a profile lease while listing.

The static demo uses representative profile names. The connected runtime can create, rename, clear, and delete profiles through `IBrowserProfileManagementService`, test a profile through `IBrowserProfileService`, and start/stop a headed interactive setup session through `IInteractiveBrowserSessionService`. Filesystem mutations and interactive setup acquire the same exclusive profile lease used by Playwright. Clear/delete require explicit confirmation and symlink/reparse-point profile directories are rejected. Clear replaces the profile contents with an empty profile directory while keeping the name available. The health check starts the selected profile headlessly and disposes it immediately after successful launch. The static demo renders all runtime actions as unavailable and never modifies profiles or starts Chromium.

#### Interactive browser sessions

The profile page can now open and close a real headed browser for a specific persistent profile on the runtime node. The primary use case is manual login, MFA, CAPTCHA handling, consent screens, and session repair without giving DABot a reusable username/password secret. This first implementation manages the browser lifecycle only; it does not yet stream the runtime-node display into the web panel.

The current local lifecycle is:

1. the operator selects a profile and chooses **Open browser**,
2. Application starts a headed Chromium session through the existing profile/session abstractions,
3. the normal profile lease prevents a run, observer, health test, mutation, or second interactive session from using that profile concurrently,
4. `IInteractiveBrowserSessionService` retains the session beyond the initiating web request and exposes active-session state to the page,
5. **Close browser** disposes the session and releases the lease,
6. if the Chromium window is closed manually or crashes, Playwright's browser-context close signal completes the session lifetime and DABot automatically removes/disposes the active-session handle,
7. every interactive session has an absolute maximum lifetime from `bot.interactiveBrowser.maxDurationSeconds` (default 1800 seconds); expiry closes the browser and releases the lease even when the operator leaves the panel.

The next stage is remote interaction: stream the browser display and keyboard/mouse input to the management panel through an authenticated remote-display bridge so the operator does not need access to the runtime node's desktop.

The management panel must not attempt to embed the target website with an `iframe`. Many authentication providers block framing, and an iframe would not represent the actual Playwright browser/profile used by the bot. A remote display implementation such as Xvfb plus VNC/noVNC over an authenticated WebSocket is an acceptable Infrastructure implementation, but the transport must remain replaceable and must not leak remote-desktop types into Core.

Security requirements:

- interactive browser access is an administrative capability and requires explicit authorization,
- the panel must use a short-lived, session-specific authorization token rather than exposing a reusable VNC credential,
- execution nodes must not expose a public VNC/noVNC port in the normal deployment,
- typed credentials, MFA codes, and page contents must not be copied into DABot configuration, scenario variables, logs, or audit payloads,
- persistent profile contents, including cookies and local storage, must be treated as sensitive authentication material,
- only one conflicting owner may use a persistent profile at a time; an interactive session must not overlap a run or observer using the same profile,
- interactive session start/end lifecycle is emitted through `IInteractiveBrowserSessionAuditSink`; the default Infrastructure adapter writes structured log events containing only session ID, profile name, timestamp, and end reason. Initial URLs are used only transiently to navigate the browser and are not retained in `InteractiveBrowserSessionInfo`; page content, credentials, MFA values, and operator keystrokes are deliberately absent from the audit contract. A durable/queryable audit store remains planned,
- interactive sessions have configurable maximum-duration cleanup; inactivity cleanup is still required before remote streaming is considered complete,
- the GitHub Pages demo must never create a real remote browser session.

A later diagnostic extension may offer **Take control** for a browser already involved in a run. That must first coordinate ownership with the runtime so automation and a human cannot issue browser commands concurrently. It must not bypass run leases, profile locks, or audit rules.

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

The dashboard remains read-only. Run details support guarded manual resume/retry/cancel actions, the run list supports filtering, scenario JSON management supports create/edit/delete and validation, and persistent browser profiles can be listed, created, renamed, cleared, deleted, and health-tested.

Visual scenario editing, synchronized visual/JSON representations, import/export, scenario test runs, clone-run actions, remote browser display/input streaming, interactive-session authorization/inactivity cleanup/durable audit storage, configuration editing, and authentication UX remain later Sprint 8/9 work.


## Static GitHub Pages demo

`DesktopAutomationBot.Web.Demo` is a Blazor WebAssembly host for the shared management UI.

Public demo:

```text
https://pawelwielga.github.io/DABot/
```

The demo registers in-memory implementations of `IRunQueryService`, `IScenarioCatalogQueryService`, and `IScenarioManagementService`, plus disabled runtime stubs for artifact and run-control services. It contains representative completed, running, waiting, failed, and cancelled runs plus valid and invalid scenario examples. Scenario edits in the demo exist only in browser memory and disappear after reload.

The demo is intentionally presentation-only:

- it does not execute Playwright,
- it does not read SQLite,
- it does not use browser profiles,
- it does not contain real credentials or runtime data,
- the top bar and banner explicitly identify demo mode.

`.github/workflows/pages-demo.yml` publishes the WASM output after relevant changes reach `main`. The deployment rewrites the base path for the repository Pages URL, creates a `404.html` SPA fallback, and adds `.nojekyll`.

Normal CI also publishes the demo on pull requests so shared-UI changes cannot merge if the static host stops compiling.
