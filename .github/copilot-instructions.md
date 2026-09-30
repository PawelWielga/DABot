# DABot repository instructions

- Read and follow `AGENTS.md`; it is the authoritative repository-wide engineering guide.
- DABot currently provides one-shot and durable declarative JSON browser automation on .NET 8 using Playwright, including persistent profiles, durable events, page observers, and a web-management panel with run controls, scenario JSON management, and browser-profile management. MCP and dynamic tools remain roadmap work unless the code and README explicitly show otherwise.
- Keep Core free of Playwright, MCP, website-specific, vendor-specific, and transport-specific types.
- Put orchestration and interfaces in Application, concrete external integrations in Infrastructure, and executable composition/CLI concerns in Runner.
- Preserve existing scenario JSON compatibility unless a migration is explicitly documented.
- For scenario JSON, use `schemas/scenario.schema.json` and prefer executable step handlers documented in README/CONTRIBUTING.
- For repository configuration, use `schemas/config.schema.json` and keep it aligned with `BotOptions`, `BrowserOptions`, and `StorageOptions`.
- Add or update tests whenever behavior changes. Keep tests Linux-friendly and headless-friendly.
- Treat README, examples, schema, docs, CI, release checklist, and discoverability metadata as part of the definition of done when a change affects them.
- Never present planned functionality as implemented.
- Never commit secrets, credentials, browser profiles, or sensitive runtime artifacts.
- Before starting new repository work, inspect the repository-wide list of open PRs, including Dependabot/bot PRs; resolve and merge them before unrelated new work.
- After creating a PR, review its diff/mergeability/checks and merge it to `main` in the same work session when correct; fix conflicts or failing CI first.
- After merging, verify the latest `main` CI and fix regressions before continuing with unrelated work.

- Keep management presentation in `DesktopAutomationBot.Web.Shared`; the real web host and GitHub Pages demo must reuse those components. The demo is sample-data only and must not execute automation or access real runtime data.
