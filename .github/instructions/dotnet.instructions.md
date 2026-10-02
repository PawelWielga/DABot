---
applyTo: "src/**/*.cs,tests/**/*.cs"
---

# .NET code instructions

- Target .NET 8 unless the repository-wide target changes.
- Keep domain models and validation in Core.
- Keep orchestration, use cases, abstractions, and step handlers in Application.
- Keep Playwright and other concrete external adapters in Infrastructure.
- Keep CLI/worker composition in Runner.
- Do not leak Playwright or MCP protocol types into Core.
- Prefer small focused handlers and interfaces over large multi-purpose classes.
- Preserve scenario JSON compatibility unless a documented migration is part of the change.
- Administrative audit models must stay metadata-only: operational IDs, timestamps, outcomes, and exception types are allowed; event payloads, correlation IDs, page content, credentials, MFA values, and operator input are not.\n- Add or update xUnit tests for behavior changes and keep them Linux/headless compatible.
- Update public documentation and examples whenever behavior or implementation status changes.
