# Durable events

DABot treats external events as durable input, not as in-memory callbacks.

## Event suspension

A durable scenario can wait for an event:

```json
{
  "id": "wait-for-approval",
  "type": "Suspend",
  "parameters": {
    "reason": "Event",
    "correlationId": "order-123",
    "eventType": "order.approved"
  }
}
```

`correlationId` is required for `Event` suspension. `eventType` is optional; when omitted, any event with the matching correlation can satisfy the wait.

Only one active event wait may own a correlation ID at a time. This prevents ambiguous delivery.

## AutomationEvent

Events have:

- stable `EventId` identity,
- a transport-neutral `Type`,
- `CorrelationId`,
- structured JSON payload,
- occurrence timestamp.

Transport adapters are intentionally outside Core. HTTP, queues, GitHub events, observers, and in-process producers can all publish the same `AutomationEvent` model through `IEventPublisher`.

## Inbox and idempotency

SQLite persists every accepted event in `AutomationEvents`.

`EventId` is the primary key. Re-delivering an already accepted event does not create another inbox record or another resume work item.

When an event matches an active wait, one SQLite transaction:

1. persists the event,
2. attaches event data to the waiting run,
3. consumes the active event wait,
4. creates a pending `ResumeWorkItem`.

The run remains `Waiting / Event` until the work item is processed. This avoids claiming that external browser work is transactionally atomic with SQLite.

## Event variables

Before resume is scheduled, the matched run receives reserved variables:

- `{{event.id}}`,
- `{{event.type}}`,
- `{{event.correlationId}}`,
- `{{event.occurredAt}}`,
- `{{event.payload}}`.

An exact `{{event.payload}}` reference preserves the structured JSON value.

## Resume work items

`IEventResumeWorker` loads pending resume work items, invokes the same durable manual-resume use case used by operator controls, then marks the work item completed or failed.

Because the work item is durable, a process can stop after accepting an event and continue processing the scheduled resume after restart.

## Current limits

The first event foundation deliberately does not yet provide:

- a concrete HTTP/queue consumer transport,
- automatic retry/backoff for failed resume work items,
- dead-letter handling,
- multi-worker claiming/leases.

Those remain separate concerns so event identity and workflow semantics do not depend on a transport implementation.
