# Browser sessions

DABot treats browser lifetime as execution-scoped state rather than a global singleton.

## Current contract

- `IBrowserSession` is the browser automation surface owned by one execution.
- `IBrowserSessionFactory` creates sessions.
- the default Playwright implementation creates a fresh ephemeral browser session per run or durable resume,
- `ScenarioExecutionContext.BrowserSession` identifies the session owned by the current execution,
- executors dispose the session in their normal `finally` path,
- concurrent runs therefore do not intentionally share a Playwright page, browser context, or browser instance.

`ScenarioExecutionContext.BrowserAutomation` remains as a compatibility view for existing step handlers while the runtime migrates to the explicit session contract.

## Durable resume

A durable resume creates a new browser session. Persisted workflow state is restored, but a previous live DOM/page is never assumed to survive process shutdown or suspension.

The current ephemeral implementation does not reconstruct login/navigation state automatically. Scenarios that need a particular page state after resume must still establish it explicitly.

## Next phase: persistent profiles

Persistent sessions will build on the same factory boundary. They must add:

- a named profile identifier,
- profile directories outside source control,
- exclusive profile locking/leases,
- headed interactive setup,
- profile health checks,
- deterministic reconstruction rules for resumed runs.

Persistent profile state must remain runtime data and must not be embedded in scenario definitions or normal run variables.
