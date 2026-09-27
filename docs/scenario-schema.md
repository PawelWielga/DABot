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
