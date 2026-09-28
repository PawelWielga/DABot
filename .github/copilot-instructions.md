# DABot repository instructions

- Read and follow `AGENTS.md`; it is the authoritative repository-wide engineering guide.
- DABot currently provides synchronous declarative JSON browser automation on .NET 8 using Playwright. Durable execution, persistent profiles, external events, web UI, MCP, and dynamic tools are roadmap work unless the code and README explicitly show otherwise.
- Keep Core free of Playwright, MCP, website-specific, vendor-specific, and transport-specific types.
- Put orchestration and interfaces in Application, concrete external integrations in Infrastructure, and executable composition/CLI concerns in Runner.
- Preserve existing scenario JSON compatibility unless a migration is explicitly documented.
- For scenario JSON, use `schemas/scenario.schema.json` and prefer executable step handlers documented in README/CONTRIBUTING.
- Add or update tests whenever behavior changes. Keep tests Linux-friendly and headless-friendly.
- Treat README, examples, schema, docs, CI, release checklist, and discoverability metadata as part of the definition of done when a change affects them.
- Never present planned functionality as implemented.
- Never commit secrets, credentials, browser profiles, or sensitive runtime artifacts.
