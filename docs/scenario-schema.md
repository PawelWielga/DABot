# Scenario schema

DABot scenario files use an explicitly versioned JSON schema.

## Current version

The current schema version is `1`.

New scenario files should declare it explicitly:

```json
{
  "schemaVersion": 1,
  "name": "Example",
  "steps": []
}
```

For backward compatibility, scenario files created before schema versioning that omit `schemaVersion` are interpreted as version 1.

A scenario that declares a schema version newer or otherwise unsupported by the running DABot version is rejected before execution. DABot must not silently reinterpret an unknown future schema.

## Step IDs

Every normalized scenario step has an ID.

New or edited scenario files should persist explicit, human-readable IDs:

```json
{
  "id": "submit-order",
  "type": "Click",
  "selector": "#submit"
}
```

Step IDs are case-insensitively unique across the entire scenario, including nested children.

Legacy scenario files may omit IDs. During load or execution, DABot assigns deterministic structural IDs such as:

```text
step-001
step-002
step-002-001
```

If a generated structural ID conflicts with an explicit ID, a deterministic numeric suffix is added.

These generated IDs preserve compatibility with existing files, but they are structural: reordering legacy steps can change them. Once a scenario is edited or exported by a future management UI, generated IDs should be persisted into the scenario definition so subsequent reordering does not change logical step identity.


## Disabled steps

Every step may define an optional `enabled` flag:

```json
{
  "id": "temporary-step",
  "type": "Screenshot",
  "enabled": false
}
```

Omitting `enabled`, or setting it to `true`, keeps the existing behavior. Setting it to `false` makes the execution engine skip the step without invoking a handler or creating a durable step attempt. Disabled `If` and `Loop` steps skip their entire child subtree.

Disabled steps remain part of the scenario definition and immutable scenario-version hash. They must still be structurally valid according to the schema and domain validation, which keeps saved definitions deterministic and allows the step to be re-enabled without changing its shape.

Durable execution persists the advanced cursor after a disabled step is skipped, so restart/resume behavior remains deterministic. Editor one-shot test runs also ignore disabled `Suspend` steps because those steps cannot suspend when they are not executable.

## Per-step retry safety

A step may override DABot's default crash-recovery classification with `retrySafety`:

```json
{
  "id": "submit-order",
  "type": "Click",
  "selector": "#submit",
  "retrySafety": "NeverRetryAutomatically"
}
```

Supported values are:

- `SafeToRetry`
- `Idempotent`
- `NeedsVerification`
- `NeverRetryAutomatically`

The field is optional. Omitting it keeps the default classification for the step type, so existing schema-v1 scenarios remain compatible.

This setting is about recovery after an interrupted/unknown attempt. It does not itself define how many normal retries are allowed; `retryCount` remains a separate concern.


## Element locators

Element-oriented steps may use either the legacy `selector` field or the richer `locator` object.

Legacy selector syntax remains fully supported:

```json
{
  "type": "Click",
  "selector": "#submit"
}
```

New scenarios may instead choose an explicit locator strategy:

```json
{
  "type": "Click",
  "locator": {
    "kind": "Text",
    "value": "Submit",
    "exact": true
  }
}
```

Supported locator kinds are:

- `Selector` — normal Playwright selector syntax;
- `Text` — Playwright `GetByText`, with optional `exact`;
- `TestId` — Playwright `GetByTestId`.

`Click`, `FillText`, `PasteText`, `ReadText`, and selector-mode `WaitFor` use the same locator abstraction. A step must not define both `selector` and `locator`, so targeting is deterministic.

Locator values support normal runtime interpolation such as `"value": "{{buttonText}}"`.

The browser abstraction keeps selector-based overloads for compatibility with existing implementations and tests. Rich locator overloads are the forward-compatible contract used by current step handlers.

## Variable interpolation

String-valued execution inputs may reference runtime variables with `{{variableName}}`.

Currently interpolated fields are:

- `selector`,
- `url`,
- `value`,
- string-valued entries in `parameters`,
- the same fields inside nested child steps.

Variables are resolved immediately before a step handler executes. The case-insensitive runtime variable bag stores JSON-compatible structured values rather than strings only. Values produced by an earlier step are therefore available to later steps without losing number/boolean/object/array types.

Embedded interpolation such as `items={{count}}` converts the value to compact text. When a string-valued entry in `parameters` is exactly `{{name}}`, DABot preserves the underlying JSON type. Durable runs may also start with legacy string variables or structured variables supplied through `ScenarioRunRequest`.

Every execution exposes its stable `runId` as a built-in string variable. An undefined variable is an execution error; DABot does not silently leave a recognized placeholder unresolved.

Secrets are deliberately not runtime variables. Secret-bearing integrations resolve named references through `ISecretProvider`; see [Runtime variables and secrets](secrets-and-variables.md).

Interpolation creates an execution-time copy of the step. It does not mutate the immutable scenario definition or change the scenario-version hash.

## Navigation synchronization

`OpenUrl` uses the browser's normal `load` event as its default navigation completion condition.

DABot intentionally does not use Playwright `NetworkIdle` as the universal default. Many modern applications keep analytics, streaming, polling, or other background requests active, so network silence is not a reliable definition of application readiness.

Scenarios that need stronger synchronization should add an explicit `WaitFor` step after `OpenUrl`. Supported waits include:

- selector visibility,
- text visibility,
- URL matching,
- load state.

`WaitFor` load-state mode may still request `networkidle` when that is explicitly appropriate for a particular workflow. The important contract is that this behavior is opt-in rather than implicit in every navigation.

## Immutable scenario versions

A durable run must execute against an immutable snapshot of a normalized scenario definition, not against a mutable scenario file that may later be edited.

`ScenarioVersion.Capture` creates that snapshot by:

1. normalizing the scenario so every step has a stable ID,
2. serializing the normalized definition into canonical JSON,
3. recursively sorting JSON object properties so dictionary/property insertion order does not affect identity,
4. computing a lowercase SHA-256 `definitionHash` from that canonical JSON,
5. storing the canonical JSON together with `ScenarioId`, `VersionId`, `VersionNumber`, schema version, and creation time.

The hash covers the normalized scenario definition only. Scenario/version database identifiers and timestamps are metadata and do not change the definition hash.

The snapshot owns its canonical JSON value. Materializing the definition returns a new object graph, so later mutation of the source scenario or of a previously materialized copy cannot change an existing version.

The persistence layer must eventually store both the canonical definition JSON and `definitionHash`. A run must reference one immutable `VersionId` for its entire lifetime. Persisting that snapshot is still part of the durable store work.

## Migration policy

Schema evolution follows these rules:

1. Existing supported schema versions are never silently reinterpreted with incompatible semantics.
2. A breaking scenario-format change requires a new `schemaVersion`.
3. Loaders reject unsupported future versions.
4. Migrations are explicit transformations from one known version to another.
5. Runtime state must reference the immutable scenario version it started with; scenario schema migration must not mutate the definition of an already-running durable workflow.
6. Fields may be added compatibly within a schema version only when omission has a deterministic backward-compatible default.

## Runtime normalization

Normalization happens before validation and execution.

Its current responsibilities are:

- preserve explicit step IDs,
- trim explicit step IDs,
- assign deterministic IDs to legacy steps that do not have one.

Normalization is intentionally separate from browser execution so future compilation steps can add execution metadata without coupling the scenario format to Playwright.


## Retry semantics

`retryCount` is optional and must be zero or greater.

It means the number of **additional** attempts after the initial execution:

| retryCount | Maximum attempts |
| ---: | ---: |
| omitted / 0 | 1 |
| 1 | 2 |
| 2 | 3 |

Durable execution persists each attempt number. Retry budgets therefore survive process restarts and cannot be reset by restarting DABot.

Retry safety is evaluated separately from the numeric budget. Having retry budget available does not make an unsafe operation automatically retryable.

### Retry delay

`retryDelayMs` is optional and must be zero or greater. It defines a fixed delay before the next durable automatic retry becomes eligible:

```json
{
  "id": "open-dashboard",
  "type": "OpenUrl",
  "url": "https://example.com",
  "retryCount": 2,
  "retryDelayMs": 5000
}
```

Omitted or `0` means no delay. DABot persists the resulting absolute due time on the run as `RetryNotBefore`, so restarting the process does not reset the delay.

Because `retryDelayMs` is optional and omitted from canonical scenario JSON when it is null, scenario-version snapshots created before this field existed remain canonical and valid within schema version 1.
