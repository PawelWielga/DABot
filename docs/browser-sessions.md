# Browser sessions

DABot treats browser lifetime as execution-scoped state rather than a global singleton.

## Session ownership

- `IBrowserSession` is the browser automation surface owned by one execution.
- `IBrowserSessionFactory` creates sessions from a `BrowserSessionRequest`.
- `ScenarioExecutionContext.BrowserSession` identifies the session owned by the current execution.
- executors dispose the session in their normal `finally` path.
- concurrent ephemeral runs receive independent Playwright browser/page/context state.

`ScenarioExecutionContext.BrowserAutomation` remains as a compatibility view for existing step handlers while the runtime migrates to the explicit session contract.

## Ephemeral sessions

When `scenario.browserProfile` is omitted, the default Playwright factory creates a fresh ephemeral browser instance and browser context for the execution.

No cookies, local storage, or login state are intentionally reused by a later run.

## Persistent named profiles

Set `browserProfile` on a scenario to select a persistent Playwright user-data directory:

```json
{
  "name": "Authenticated workflow",
  "browserProfile": "work-account",
  "steps": []
}
```

Profile names are deliberately path-safe: they must start with an alphanumeric character, contain only letters, digits, `.`, `_`, or `-`, and are limited to 64 characters.

Persistent profile data is stored under `bot.storage.browserProfilesDirectory`, which defaults to `data/browser-profiles`. The repository ignores `data/`, so cookies and browser storage remain runtime data rather than source-controlled scenario data.

A file lease under the profile root prevents two processes or runs from opening the same named profile at the same time. Different profile names can be used concurrently.

## Durable resume

A durable resume always acquires a new `IBrowserSession`.

- ephemeral runs rebuild a new blank browser state,
- profile-backed runs reopen the named persistent user-data directory,
- persisted workflow state never assumes that an old in-memory DOM/page survived suspension or process shutdown.

Persistent profiles restore browser-managed state such as cookies and local storage, but they do not guarantee that the browser reopens on the exact page required by the next workflow step. Scenarios should still establish required navigation/wait state explicitly.

## Interactive setup and profile health

Use a headed setup session when a profile needs manual login, consent, MFA, or other one-time browser interaction:

```bash
dotnet run --project src/DesktopAutomationBot.Runner -- profile setup --profile work-account --url https://example.com/login
```

The runner forces headed mode for that session even if normal automation is configured as headless. Complete the setup in the browser, then press Enter in the terminal to close the browser cleanly and persist browser-managed state.

The web management profile page can also start a headed setup session on the runtime node. `IInteractiveBrowserSessionService` keeps that browser alive independently of the initiating UI event, lists active sessions, and can close them explicitly. The session uses the normal persistent-profile lease, so it cannot overlap a run, observer, profile health test, or profile mutation for the same profile.

`IInteractiveBrowserSession` exposes a transport-neutral completion signal. The Playwright implementation completes it from `BrowserContext.Close`, so manually closing or crashing the headed browser triggers cleanup and releases the profile lease.

Interactive browser sessions also have a configurable absolute lifetime through `bot.interactiveBrowser.maxDurationSeconds` (default 1800 seconds). The Application lifecycle service schedules expiry independently of the UI; when the deadline is reached it removes the active session, closes Chromium, and releases the profile lease.

Use the health command to verify that a profile directory can be leased and launched:

```bash
dotnet run --project src/DesktopAutomationBot.Runner -- profile test --profile work-account
```

The health check opens the named profile headlessly and disposes it immediately after successful startup. A profile already leased by another process fails the test rather than bypassing the exclusive lock.

## Remaining profile work

Sprint 3 session/runtime foundations are complete. The web-management layer now supports profile list/create/rename/clear/delete, health testing, and local headed interactive-session lifecycle. Remote display/input streaming, authorization/session grants, inactivity cleanup, and audit remain planned. Maximum-duration cleanup is implemented.
