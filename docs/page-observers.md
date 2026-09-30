# Generic page observers

Page observers monitor browser-visible state independently of a scenario run and publish the same durable `AutomationEvent` model used by other event sources.

## Import and run

Import a JSON definition:

```bash
dotnet run --project src/DesktopAutomationBot.Runner -- observer import --file examples/page-observer.json
```

Run the polling worker:

```bash
dotnet run --project src/DesktopAutomationBot.Runner -- observer-worker
```

Definitions and polling state are persisted in the same SQLite runtime database, so restarting the worker does not reset change baselines or edge-trigger state.

## Conditions

Supported conditions:

- `SelectorVisible`
- `SelectorHidden`
- `TextEquals`
- `TextContains`
- `TextChanged`
- `UrlMatches`

Locator-based conditions reuse the scenario locator model, including selector, text, and test-id locators.

`UrlMatches` treats `expectedValue` as a regular expression.

## Event behavior

Each observer defines an `eventType` and `correlationId`.

For visible/hidden/equality/contains/URL conditions, events are edge-triggered: a matching state emits once and remains silent until the condition becomes false and matches again.

`TextChanged` stores the first observed text as a baseline without emitting an event. Later changes emit an event and replace the baseline.

Observer event payloads include the observer ID, observer name, condition, current observation, match result, and check timestamp.

## Browser profiles

Set `browserProfile` to reuse a persistent authenticated browser profile. When omitted, each poll uses an ephemeral session.

Profile locking remains enforced by the normal browser session factory, so an observer cannot silently share a named profile with another active process.

## Polling and backoff

Each definition has `pollIntervalMs`.

The worker scans for due observers according to the persisted `NextCheckAt`. Successful checks schedule the next poll using the observer interval.

Failures preserve the previous successful observation, increment `FailureCount`, record the error, and apply exponential backoff capped by `bot.observerWorker.maxErrorBackoffMs`.

Worker scan settings live under:

```json
{
  "bot": {
    "observerWorker": {
      "pollIntervalMs": 1000,
      "batchSize": 50,
      "maxErrorBackoffMs": 60000
    }
  }
}
```

## Current limit

Optional DOM-fragment/hash change detection remains planned. Existing text and visibility conditions cover the first generic observer runtime without coupling observation semantics to a specific transport or scenario.
