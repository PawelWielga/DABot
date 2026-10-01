---
applyTo: "scenarios/**/*.json,examples/**/*.json,schemas/**/*.json"
---

# Scenario JSON instructions

- Treat `schemas/scenario.schema.json` as the machine-readable scenario contract.
- Current executable steps are `OpenUrl`, `Click`, `FillText`, `PasteText`, `ReadText`, `WaitFor`, `Delay`, `Screenshot`, `CallApi`, `If`, and `Loop`; `Suspend` is executable only through the durable run path.
- A step may set `enabled: false`. Omitted/true means enabled; disabled steps are skipped without handler invocation, and disabled `If`/`Loop` steps skip their entire child subtree.
- String scenario fields support `{{variableName}}` interpolation from prior outputs/run variables; `runId` is built in.
- When changing the scenario contract, update the Core model/validator, schema, examples, tests, README project-status table, and backlog together.
- Keep examples deterministic and self-contained where practical; avoid unnecessary dependence on third-party test sites.
