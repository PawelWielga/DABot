# DABot repository instructions

- Read and follow `AGENTS.md`; it is the authoritative repository-wide engineering guide.
- DABot currently provides one-shot and durable declarative JSON browser automation on .NET 8 using Playwright, including persistent profiles, durable events, page observers, and a web-management panel with run controls, scenario JSON management, browser-profile management/local headed interactive-profile lifecycle, observer monitoring/create/edit management, event-history browsing without raw payload exposure, manual durable-event publishing through Application services, and metadata-only structured audit logging for manual resume/cancel/event operations. MCP and dynamic tools remain roadmap work unless the code and README explicitly show otherwise.
- Keep Core free of Playwright, MCP, website-specific, vendor-specific, and transport-specific types.
- Put orchestration and interfaces in Application, concrete external integrations in Infrastructure, and executable composition/CLI concerns in Runner.
- Stable node identity is implemented through `INodeIdentityProvider`; the default file adapter persists the machine-local GUID at `bot.node.identityPath`. Preserve it across restarts and never share one identity file between distinct nodes.
- Preserve existing scenario JSON compatibility unless a migration is explicitly documented.
- For scenario JSON, use `schemas/scenario.schema.json` and prefer executable step handlers documented in README/CONTRIBUTING.
- For repository configuration, use `schemas/config.schema.json` and keep it aligned with `BotOptions` and its nested option types, including `NodeOptions`, `BrowserOptions`, and `StorageOptions`.
- Add or update tests whenever behavior changes. Keep tests Linux-friendly and headless-friendly.
- Treat README, examples, schema, docs, CI, release checklist, and discoverability metadata as part of the definition of done when a change affects them.
- Never present planned functionality as implemented.
- Never commit secrets, credentials, browser profiles, or sensitive runtime artifacts.
- Before starting new repository work, inspect the repository-wide list of open PRs, including Dependabot/bot PRs; resolve and merge them before unrelated new work.
- After creating a PR, review its diff/mergeability/checks and merge it to `main` in the same work session when correct; fix conflicts or failing CI first.
- After merging, verify the latest `main` CI and fix regressions before continuing with unrelated work.

- Keep management presentation in `DesktopAutomationBot.Web.Shared`; the real web host and GitHub Pages demo must reuse those components. The demo is sample-data only and must not execute automation or access real runtime data.

- Keep interactive-browser session and audit contracts metadata-only. Operator input must go directly to the browser/profile; never add target URLs with sensitive query data, page content, form values, credentials, MFA values, or operator keystrokes to Application models, logs, configuration, or durable variables.
- Keep administrative audit contracts metadata-only as well: no event payloads, correlation IDs, page content, credentials, MFA values, or operator input.
