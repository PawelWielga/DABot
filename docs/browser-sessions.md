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

## Remaining profile work

The session boundary now supports ephemeral and persistent named profiles with exclusive leases. Remaining management work includes:

- headed interactive profile setup,
- profile health/test operations,
- explicit profile management in the future web UI.
