---
applyTo: "scenarios/**/*.json,examples/**/*.json,schemas/**/*.json"
---

# Scenario JSON instructions

- Treat `schemas/scenario.schema.json` as the machine-readable scenario contract.
- Runnable scenarios should use currently executable steps: `OpenUrl`, `Click`, `FillText`, `PasteText`, `ReadText`, `WaitFor`, and `Screenshot`.
- `CallApi`, `Delay`, `If`, and `Loop` exist in the current domain enum but do not yet have registered executable handlers.
- When changing the scenario contract, update the Core model/validator, schema, examples, tests, README project-status table, and backlog together.
- Keep examples deterministic and self-contained where practical; avoid unnecessary dependence on third-party test sites.
