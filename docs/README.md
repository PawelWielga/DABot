# DABot documentation

This directory contains design and implementation documentation. Use this page as the navigation map instead of guessing which document is authoritative.

## Start here

| Need | Document |
| --- | --- |
| Understand what DABot does today | [README](../README.md) |
| Contribute code or documentation | [CONTRIBUTING](../CONTRIBUTING.md) |
| Instructions for coding/AI agents | [AGENTS](../AGENTS.md) |
| Generate or validate scenario JSON | [Scenario JSON Schema](../schemas/scenario.schema.json) |
| Generate or validate runtime configuration | [Configuration JSON Schema](../schemas/config.schema.json) |
| Understand product requirements | [Product requirements](prd.md) |
| Understand product positioning and differentiation | [Product positioning](product-positioning.md) |
| See implementation status and order | [Implementation backlog](tasks.md) |
| Publish and smoke-test DABot on Linux | [Linux publish and smoke test](linux-publish.md) |
| Understand durable execution design | [Durable workflows](durable-workflows.md) |
| Understand MCP and dynamic tools direction | [MCP and dynamic tools](mcp-and-dynamic-tools.md) |
| Compare DABot with raw Playwright | [Comparison](comparison.md) |

## Status model

The repository intentionally separates current implementation from future architecture.

- `README.md` is the concise public source for what works now.
- `docs/tasks.md` is the implementation-status source of truth.
- architecture documents may describe planned behavior that is not implemented yet.
- examples under `examples/` should use executable behavior unless explicitly labeled otherwise.
- JSON Schemas describe the scenario and configuration contracts; the scenario schema labels domain values that are not yet executable.

When implementation changes, update all affected sources in the same change so they do not contradict each other.

## Historical implementation notes

- [Sprint 0 implementation](sprint-0-implementation.md)
- [Sprint 1 implementation](sprint-1-implementation.md)

These files document completed slices of work. Current status should be taken from the main README and backlog rather than inferred from historical notes.
